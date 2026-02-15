using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.Chat;
using System.Security.Claims;

namespace ResetYourFuture.Client.Pages;

public partial class Chat : IAsyncDisposable
{
    [Inject] private IChatService ChatService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private List<ChatConversationDto>? _conversations;
    private List<ChatMessageDto> _messages = [];
    private ChatConversationDto? _selectedConversation;
    private string _newMessage = string.Empty;
    private string _currentUserId = string.Empty;
    private bool _isLoadingMessages;
    private bool _isStarting;
    private ElementReference _messageContainer;

    // --- User Picker ---
    private bool _showUserPicker;
    private string _userSearchTerm = string.Empty;
    private List<ChatUserDto> _availableUsers = [];
    private bool _isSearching;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _currentUserId = user.FindFirst( ClaimTypes.NameIdentifier )?.Value ?? string.Empty;

        ChatService.OnMessageReceived += HandleMessageReceived;
        ChatService.OnNotificationReceived += HandleNotification;

        await ChatService.StartAsync();
        _conversations = await ChatService.GetConversationsAsync();
    }

    private async Task SelectConversation( ChatConversationDto conversation )
    {
        _selectedConversation = conversation;
        _isLoadingMessages = true;
        StateHasChanged();

        _messages = await ChatService.GetMessagesAsync( conversation.Id );
        await ChatService.MarkAsReadAsync( conversation.Id );

        // Update unread count in list.
        UpdateConversationUnread( conversation.Id , 0 );

        _isLoadingMessages = false;
        StateHasChanged();
    }

    // --- User Picker ---

    private async Task OpenUserPicker()
    {
        _showUserPicker = true;
        _userSearchTerm = string.Empty;
        _availableUsers = [];
        StateHasChanged();

        // Load initial list.
        await SearchUsersAsync();
    }

    private void CloseUserPicker()
    {
        _showUserPicker = false;
        _userSearchTerm = string.Empty;
        _availableUsers = [];
    }

    private async Task HandleSearchKeyDown( KeyboardEventArgs e )
    {
        // Debounce: cancel previous search, start new one.
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay( 300 , token );
            await SearchUsersAsync();
        }
        catch ( TaskCanceledException )
        {
            // Expected when typing quickly.
        }
    }

    private async Task SearchUsersAsync()
    {
        _isSearching = true;
        StateHasChanged();

        _availableUsers = await ChatService.GetAvailableUsersAsync( _userSearchTerm );

        _isSearching = false;
        StateHasChanged();
    }

    private async Task PickUser( ChatUserDto user )
    {
        _showUserPicker = false;
        _isStarting = true;
        StateHasChanged();

        var conversation = await ChatService.StartConversationWithAsync( user.Id );
        if ( conversation is not null )
        {
            // Add to list if not already present.
            var existing = _conversations?.FirstOrDefault( c => c.Id == conversation.Id );
            if ( existing is null )
            {
                _conversations?.Insert( 0 , conversation );
            }

            await SelectConversation( conversation );
        }

        _isStarting = false;
        StateHasChanged();
    }

    // --- Messaging ---

    private async Task SendMessage()
    {
        if ( _selectedConversation is null || string.IsNullOrWhiteSpace( _newMessage ) )
            return;

        await ChatService.SendMessageAsync( _selectedConversation.Id , _newMessage );
        _newMessage = string.Empty;
    }

    private async Task HandleKeyDown( KeyboardEventArgs e )
    {
        if ( e.Key == "Enter" && !string.IsNullOrWhiteSpace( _newMessage ) )
        {
            await SendMessage();
        }
    }

    private void HandleMessageReceived( ChatMessageDto message )
    {
        InvokeAsync( () =>
        {
            if ( _selectedConversation is not null && message.ConversationId == _selectedConversation.Id )
            {
                _messages.Add( message );

                // Auto-mark as read if we're viewing this conversation.
                if ( message.SenderId != _currentUserId )
                {
                    _ = ChatService.MarkAsReadAsync( message.ConversationId );
                }
            }
            else
            {
                // Increment unread for that conversation.
                UpdateConversationUnread( message.ConversationId , null );
            }

            // Update last message in sidebar.
            if ( _conversations is not null )
            {
                var idx = _conversations.FindIndex( c => c.Id == message.ConversationId );
                if ( idx >= 0 )
                {
                    var old = _conversations[idx];
                    _conversations[idx] = old with
                    {
                        LastMessageContent = message.Content ,
                        LastMessageAt = message.SentAt
                    };

                    // Move to top.
                    if ( idx > 0 )
                    {
                        var item = _conversations[idx];
                        _conversations.RemoveAt( idx );
                        _conversations.Insert( 0 , item );
                    }
                }
            }

            StateHasChanged();
        } );
    }

    private void HandleNotification( ChatNotificationDto notification )
    {
        // Notification handling is done via HandleMessageReceived above.
        // This hook is available for toast/audio in the future.
    }

    private void UpdateConversationUnread( Guid conversationId , int? explicitCount )
    {
        if ( _conversations is null ) return;

        var idx = _conversations.FindIndex( c => c.Id == conversationId );
        if ( idx < 0 ) return;

        var old = _conversations[idx];
        var newCount = explicitCount ?? ( old.UnreadCount + 1 );
        _conversations[idx] = old with { UnreadCount = newCount };
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        ChatService.OnMessageReceived -= HandleMessageReceived;
        ChatService.OnNotificationReceived -= HandleNotification;
        await ChatService.DisposeAsync();
    }
}
