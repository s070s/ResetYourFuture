using Microsoft.AspNetCore.SignalR.Client;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Application.ApiInterfaces;
using ResetYourFuture.Application.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Chat service combining REST API calls (history) and SignalR (real-time).
///
/// The hub URL is captured from IHttpContextAccessor during construction (before the Blazor Server
/// circuit transitions away from the HTTP request context). Bearer tokens are minted per call —
/// REST calls via <see cref="ApiTokenProvider"/> and the SignalR hub via its AccessTokenProvider —
/// because HttpContext (and therefore <c>SsrApiHandler</c>) is null inside the interactive circuit,
/// where these calls would otherwise go out unauthenticated and 401.
/// </summary>
public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly string _hubUrl;
    private readonly IAuthService _authService;
    private readonly ApiTokenProvider _tokenProvider;
    private readonly ILogger<ChatService> _logger;
    private HubConnection? _hub;

    public event Action<ChatMessageDto>? OnMessageReceived;
    public event Action<ChatNotificationDto>? OnNotificationReceived;
    public event Action? ConnectionStateChanged;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public ChatService(HttpClient http, IAuthService authService, ApiTokenProvider tokenProvider, IHttpContextAccessor httpContextAccessor, ILogger<ChatService> logger)
    {
        _http = http;
        _authService = authService;
        _tokenProvider = tokenProvider;
        _logger = logger;

        // Capture hub URL from the current HTTP request before circuit transition.
        var request = httpContextAccessor.HttpContext?.Request;
        var scheme = request?.Scheme ?? "https";
        var host = request?.Host.ToString() ?? "localhost:7090";
        _hubUrl = $"{scheme}://{host}/hubs/chat";
    }

    public async Task StartAsync(System.Security.Claims.ClaimsPrincipal? circuitUser = null)
    {
        if (_hub is not null)
            return;

        _hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                // Mint a fresh token on every call so reconnects after the JWT expiry succeed.
                // HttpContext is null inside Blazor Server circuits, so always use the cascaded
                // principal when one was supplied.
                options.AccessTokenProvider = () => circuitUser is not null
                    ? _authService.GetTokenAsync(circuitUser)
                    : _authService.GetTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<ChatMessageDto>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        _hub.On<ChatNotificationDto>("ChatNotification", notification =>
        {
            OnNotificationReceived?.Invoke(notification);
        });

        // Surface reconnect lifecycle so the UI can show a "reconnecting" indicator.
        _hub.Reconnecting += _ => { ConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
        _hub.Reconnected += _ => { ConnectionStateChanged?.Invoke(); return Task.CompletedTask; };
        _hub.Closed += _ => { ConnectionStateChanged?.Invoke(); return Task.CompletedTask; };

        try
        {
            await _hub.StartAsync();
            ConnectionStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR hub connection to {HubUrl}.", _hubUrl);
            await _hub.DisposeAsync();
            _hub = null;
            ConnectionStateChanged?.Invoke();
        }
    }

    public async Task StopAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    /// <summary>
    /// Attaches a freshly minted bearer token (or clears it when anonymous) to <see cref="_http"/>
    /// before a REST call. <see cref="ApiTokenProvider"/> reads the principal from
    /// <c>AuthenticationStateProvider</c>, which — unlike <c>IHttpContextAccessor</c> /
    /// <c>SsrApiHandler</c> — is available inside the interactive Blazor Server circuit. The token is
    /// identical for every call within a circuit, so mutating the default header is safe on the
    /// single-threaded circuit dispatcher. ChatService does not extend <c>ApiClientBase</c>, so it
    /// attaches the token itself.
    /// </summary>
    private async Task EnsureAuthorizationAsync()
    {
        var token = await _tokenProvider.GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization =
            string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<PagedResult<ChatConversationDto>> GetConversationsAsync(int page = 1, int pageSize = 10)
    {
        try
        {
            await EnsureAuthorizationAsync();
            var response = await _http.GetAsync($"api/chat/conversations?page={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<ChatConversationDto>>()
                       ?? new PagedResult<ChatConversationDto>([], 0, page, pageSize);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch conversations (page={Page}).", page);
        }
        return new PagedResult<ChatConversationDto>([], 0, page, pageSize);
    }

    public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync(Guid conversationId, int page = 1, int pageSize = 20)
    {
        try
        {
            await EnsureAuthorizationAsync();
            var response = await _http.GetAsync(
                $"api/chat/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<ChatMessageDto>>()
                       ?? new PagedResult<ChatMessageDto>([], 0, page, pageSize);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch messages for conversation {ConversationId} (page={Page}).", conversationId, page);
        }
        return new PagedResult<ChatMessageDto>([], 0, page, pageSize);
    }

    public async Task<ChatConversationDto?> StartConversationWithAsync(string targetUserId, string? initialMessage = null)
    {
        try
        {
            await EnsureAuthorizationAsync();
            var request = new StartConversationRequest(targetUserId, initialMessage);
            var response = await _http.PostAsJsonAsync("api/chat/conversations/start", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatConversationDto>();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to start conversation with user {TargetUserId}.", targetUserId);
        }
        return null;
    }

    public async Task<List<ChatUserDto>> GetAvailableUsersAsync(string? search = null)
    {
        try
        {
            await EnsureAuthorizationAsync();
            var url = string.IsNullOrWhiteSpace(search)
                ? "api/chat/users"
                : $"api/chat/users?search={Uri.EscapeDataString(search)}";

            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ChatUserDto>>() ?? [];
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch available chat users (search={Search}).", search);
        }
        return [];
    }

    public async Task SendMessageAsync(Guid conversationId, string content)
    {
        if (_hub is not null && IsConnected)
        {
            await _hub.InvokeAsync("SendMessage", conversationId, content);
        }
    }

    public async Task MarkAsReadAsync(Guid conversationId)
    {
        if (_hub is not null && IsConnected)
        {
            await _hub.InvokeAsync("MarkAsRead", conversationId);
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            await EnsureAuthorizationAsync();
            var response = await _http.GetAsync("api/chat/unread-count");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<int>();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch unread message count.");
        }
        return 0;
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId)
    {
        try
        {
            await EnsureAuthorizationAsync();
            var response = await _http.DeleteAsync($"api/chat/conversations/{conversationId}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete conversation {ConversationId}.", conversationId);
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
