// WebRTC signaling + media interop for Blazor video calls (see VIDEO_CALL_PLAN.md, WP4).
// Peer connections are keyed by SignalR connectionId ("peerKey"). Offer/answer glare is
// resolved with the perfect-negotiation pattern: each peer is assigned a polite/impolite
// role (CallService decides, based on connectionId ordering) and the impolite side always
// wins a collision by ignoring the incoming offer.
window.webrtcInterop = {
    _dotNetRef: null,
    _iceServers: [],
    _localStream: null,
    _screenStream: null,
    _screenTrack: null,
    _peers: {},

    // Must be called once before any other method. iceServers is the WebRtcOptions.IceServers
    // list (each { urls, username, credential }) sent down from the server.
    init: function (dotNetRef, iceServers) {
        this._dotNetRef = dotNetRef;
        this._iceServers = (iceServers || []).map(function (s) {
            return { urls: s.urls, username: s.username, credential: s.credential };
        });
    },

    // Returns 'full' (audio+video), 'audio' (camera unavailable — none present, or held by
    // another app/browser on the same machine — joined audio-only), or 'none' (no media at all).
    startLocalMedia: async function (audio, video) {
        try {
            this._localStream = await navigator.mediaDevices.getUserMedia({ audio: audio, video: video });
            return 'full';
        } catch (err) {
            if (video) {
                try {
                    this._localStream = await navigator.mediaDevices.getUserMedia({ audio: audio, video: false });
                    return 'audio';
                } catch (audioErr) {
                    await this._reportError('startLocalMedia', null, audioErr);
                    return 'none';
                }
            }
            await this._reportError('startLocalMedia', null, err);
            return 'none';
        }
    },

    // Creates the RTCPeerConnection for one remote participant and wires up perfect
    // negotiation. `polite` decides who yields on an offer/answer collision.
    createPeer: function (peerKey, polite) {
        if (this._peers[peerKey]) {
            this.closePeer(peerKey);
        }

        const self = this;
        const pc = new RTCPeerConnection({ iceServers: this._iceServers });
        const peer = {
            pc: pc,
            polite: polite,
            makingOffer: false,
            ignoreOffer: false,
            isSettingRemoteAnswerPending: false,
            iceRestarted: false,
            pendingCandidates: [],
            remoteStream: null
        };
        this._peers[peerKey] = peer;

        if (this._localStream) {
            this._localStream.getTracks().forEach(function (track) {
                pc.addTrack(track, self._localStream);
            });
        }

        pc.onnegotiationneeded = function () {
            self._negotiate(peerKey);
        };

        pc.onicecandidate = function (e) {
            if (e.candidate) {
                self._dotNetRef.invokeMethodAsync('OnIceCandidate', peerKey, JSON.stringify(e.candidate));
            }
        };

        pc.ontrack = function (e) {
            peer.remoteStream = e.streams[0] || peer.remoteStream;
            const el = document.getElementById('video-' + peerKey);
            if (el && peer.remoteStream) el.srcObject = peer.remoteStream;
            self._dotNetRef.invokeMethodAsync('OnRemoteStreamChanged', peerKey);
        };

        pc.onconnectionstatechange = function () {
            self._dotNetRef.invokeMethodAsync('OnPeerConnectionState', peerKey, pc.connectionState);
        };

        return true;
    },

    // Explicitly triggers the initial offer for a newly created peer (mesh-join convention:
    // the newcomer offers to each existing participant).
    initiateOffer: async function (peerKey) {
        await this._negotiate(peerKey);
    },

    // Applies an offer/answer received via the hub. For offers, replies with an answer
    // through the OnLocalDescription callback.
    setRemoteDescription: async function (peerKey, type, sdp) {
        const peer = this._peers[peerKey];
        if (!peer) return;
        const pc = peer.pc;
        const description = { type: type, sdp: sdp };

        const readyForOffer = !peer.makingOffer &&
            (pc.signalingState === 'stable' || peer.isSettingRemoteAnswerPending);
        const offerCollision = type === 'offer' && !readyForOffer;

        peer.ignoreOffer = !peer.polite && offerCollision;
        if (peer.ignoreOffer) return;

        // A stale/duplicate answer for a negotiation that already completed (glare aftermath) —
        // applying it in 'stable' throws InvalidStateError and there is nothing to act on.
        if (type === 'answer' && pc.signalingState === 'stable') return;

        try {
            peer.isSettingRemoteAnswerPending = type === 'answer';
            if (offerCollision) {
                await Promise.all([
                    pc.setLocalDescription({ type: 'rollback' }),
                    pc.setRemoteDescription(description)
                ]);
            } else {
                await pc.setRemoteDescription(description);
            }
            peer.isSettingRemoteAnswerPending = false;

            await this._drainPendingCandidates(peer);

            if (type === 'offer') {
                await pc.setLocalDescription();
                await this._dotNetRef.invokeMethodAsync('OnLocalDescription', peerKey, pc.localDescription.type, pc.localDescription.sdp);
            }
        } catch (err) {
            await this._reportError('setRemoteDescription', peerKey, err);
        }
    },

    // Candidates are queued until the remote description is set (can arrive before the
    // offer/answer over an unordered signaling path).
    addIceCandidate: async function (peerKey, candidateJson) {
        const peer = this._peers[peerKey];
        if (!peer) return;
        const candidate = JSON.parse(candidateJson);

        if (!peer.pc.remoteDescription) {
            peer.pendingCandidates.push(candidate);
            return;
        }

        try {
            await peer.pc.addIceCandidate(candidate);
        } catch (err) {
            if (!peer.ignoreOffer) {
                await this._reportError('addIceCandidate', peerKey, err);
            }
        }
    },

    // One-shot ICE restart, invoked by CallService after OnPeerConnectionState reports
    // "failed"; triggers onnegotiationneeded, which re-runs the normal offer path.
    restartIce: function (peerKey) {
        const peer = this._peers[peerKey];
        if (!peer || peer.iceRestarted) return;
        peer.iceRestarted = true;
        try {
            peer.pc.restartIce();
        } catch (err) {
            this._reportError('restartIce', peerKey, err);
        }
    },

    closePeer: function (peerKey) {
        const peer = this._peers[peerKey];
        if (!peer) return;
        peer.pc.onnegotiationneeded = null;
        peer.pc.onicecandidate = null;
        peer.pc.ontrack = null;
        peer.pc.onconnectionstatechange = null;
        peer.pc.close();
        delete this._peers[peerKey];
    },

    closeAll: function () {
        const self = this;
        Object.keys(this._peers).forEach(function (peerKey) {
            self.closePeer(peerKey);
        });
        if (this._screenStream) {
            this._screenStream.getTracks().forEach(function (track) { track.stop(); });
            this._screenStream = null;
            this._screenTrack = null;
        }
        if (this._localStream) {
            this._localStream.getTracks().forEach(function (track) { track.stop(); });
            this._localStream = null;
        }
    },

    setMicEnabled: function (enabled) {
        if (!this._localStream) return;
        this._localStream.getAudioTracks().forEach(function (track) { track.enabled = enabled; });
    },

    // Returns whether any local video track exists — false when the call was joined audio-only,
    // so CallService can keep CameraOn=false instead of showing a lying "camera on" state.
    setCameraEnabled: function (enabled) {
        if (!this._localStream) return false;
        const tracks = this._localStream.getVideoTracks();
        tracks.forEach(function (track) { track.enabled = enabled; });
        return tracks.length > 0;
    },

    // Replaces the outgoing video track on every peer with the shared-screen track (no
    // renegotiation needed). Auto-reverts if the user stops sharing from the browser chrome.
    startScreenShare: async function (localVideoElementId) {
        try {
            this._screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true });
            const screenTrack = this._screenStream.getVideoTracks()[0];
            this._screenTrack = screenTrack;

            const self = this;
            Object.keys(this._peers).forEach(function (peerKey) {
                const sender = self._peers[peerKey].pc.getSenders().find(function (s) {
                    return s.track && s.track.kind === 'video';
                });
                if (sender) sender.replaceTrack(screenTrack);
            });

            screenTrack.onended = function () {
                self.stopScreenShare(localVideoElementId);
            };

            if (localVideoElementId) {
                const el = document.getElementById(localVideoElementId);
                if (el) el.srcObject = this._screenStream;
            }

            return true;
        } catch (err) {
            await this._reportError('startScreenShare', null, err);
            return false;
        }
    },

    stopScreenShare: async function (localVideoElementId) {
        const self = this;
        const originalTrack = this._localStream ? this._localStream.getVideoTracks()[0] : null;

        Object.keys(this._peers).forEach(function (peerKey) {
            const sender = self._peers[peerKey].pc.getSenders().find(function (s) {
                return s.track === self._screenTrack;
            });
            if (sender && originalTrack) sender.replaceTrack(originalTrack);
        });

        if (this._screenTrack) {
            this._screenTrack.stop();
            this._screenTrack = null;
        }
        this._screenStream = null;

        if (localVideoElementId) {
            const el = document.getElementById(localVideoElementId);
            if (el && this._localStream) el.srcObject = this._localStream;
        }

        if (this._dotNetRef) {
            await this._dotNetRef.invokeMethodAsync('OnScreenShareEnded');
        }
    },

    bindLocalVideo: function (elementId) {
        const el = document.getElementById(elementId);
        if (el && this._localStream) el.srcObject = this._localStream;
    },

    bindRemoteVideo: function (peerKey, elementId) {
        const peer = this._peers[peerKey];
        const el = document.getElementById(elementId);
        if (!peer || !el || !peer.remoteStream) return;
        el.srcObject = peer.remoteStream;
    },

    _negotiate: async function (peerKey) {
        const peer = this._peers[peerKey];
        if (!peer) return;
        // A negotiation is already in flight (e.g. the explicit initiateOffer ran and the
        // addTrack-triggered onnegotiationneeded fires right after) — re-offering here would send
        // a duplicate offer and earn a stale second answer back.
        if (peer.makingOffer || peer.pc.signalingState !== 'stable') return;
        try {
            peer.makingOffer = true;
            await peer.pc.setLocalDescription();
            await this._dotNetRef.invokeMethodAsync('OnLocalDescription', peerKey, peer.pc.localDescription.type, peer.pc.localDescription.sdp);
        } catch (err) {
            await this._reportError('negotiate', peerKey, err);
        } finally {
            peer.makingOffer = false;
        }
    },

    _drainPendingCandidates: async function (peer) {
        while (peer.pendingCandidates.length) {
            const candidate = peer.pendingCandidates.shift();
            try {
                await peer.pc.addIceCandidate(candidate);
            } catch (err) {
                // A queued candidate can belong to a superseded negotiation (rollback or a
                // renegotiation changed the ICE generation). Dropping it is harmless; throwing
                // here would abort setRemoteDescription before the answer is created and wedge
                // the whole handshake.
                console.warn('webrtcInterop: dropped stale queued ICE candidate', err);
            }
        }
    },

    _reportError: async function (context, peerKey, err) {
        console.error('webrtcInterop.' + context, peerKey, err);
        if (this._dotNetRef) {
            try {
                await this._dotNetRef.invokeMethodAsync('OnMediaError', context, peerKey, err && err.message ? err.message : String(err));
            } catch (invokeErr) {
                // .NET side (circuit/dotNetRef) already gone — nothing left to report to.
            }
        }
    }
};
