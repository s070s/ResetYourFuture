using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using ResetYourFuture.Web.Consumers;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Web.Pages;

public partial class Chat : IAsyncDisposable
{
    [Inject] private IChatService ChatService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private bool _chatAccess;
    private bool _accessChecked;
    private PagedResult<ChatConversationDto>? _conversations;
    private int _conversationsPage = 1;
    private int _conversationsPageSize = 10;
    private static readonly int[] ConversationPageSizeOptions = [10, 25, 50];
    private PagedResult<ChatMessageDto>? _pagedMessages;
    private int _messagesPage = 1;
    private int _messagesPageSize = 20;
    private static readonly int[] MessagePageSizeOptions = [10, 20, 50];
    private ChatConversationDto? _selectedConversation;
    private string _currentUserId = string.Empty;
    private bool _isLoadingMessages;
    private bool _isStarting;

    // --- User Picker ---
    private bool _showUserPicker;

    // --- Delete Conversation ---
    private ChatConversationDto? _conversationToDelete;
    private bool _isDeleting;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _currentUserId = user.FindFirst( ClaimTypes.NameIdentifier )?.Value ?? string.Empty;

        var isAdmin = user.IsInRole( "Admin" );
        if ( isAdmin )
        {
            _chatAccess = true;
        }
        else
        {
            var status = await SubscriptionService.GetStatusAsync();
            _chatAccess = status?.Features?.PrioritySupport == true;
        }

        _accessChecked = true;

        if ( !_chatAccess )
            return;

        ChatService.OnMessageReceived += HandleMessageReceived;
        ChatService.OnNotificationReceived += HandleNotification;

        await ChatService.StartAsync( user );
        await LoadConversationsAsync();
    }

    private async Task SelectConversation( ChatConversationDto conversation )
    {
        _selectedConversation = conversation;
        _messagesPage = 1;
        _isLoadingMessages = true;
        StateHasChanged();

        await LoadMessagesAsync();

        // Jump straight to the last (newest) page.
        if ( _pagedMessages is { TotalPages: > 1 } )
        {
            _messagesPage = _pagedMessages.TotalPages;
            await LoadMessagesAsync();
        }

        await ChatService.MarkAsReadAsync( conversation.Id );
        UpdateConversationUnread( conversation.Id , 0 );

        _isLoadingMessages = false;
        StateHasChanged();
    }

    private async Task LoadConversationsAsync()
    {
        _conversations = await ChatService.GetConversationsAsync( _conversationsPage , _conversationsPageSize );
    }

    private async Task LoadMessagesAsync()
    {
        if ( _selectedConversation is null ) return;
        _pagedMessages = await ChatService.GetMessagesAsync( _selectedConversation.Id , _messagesPage , _messagesPageSize );
    }

    private async Task GoToMessagePage( int page )
    {
        _messagesPage = page;
        _isLoadingMessages = true;
        StateHasChanged();
        await LoadMessagesAsync();
        _isLoadingMessages = false;
        StateHasChanged();
    }

    private async Task PreviousMessagePage()
    {
        if ( _messagesPage > 1 )
            await GoToMessagePage( _messagesPage - 1 );
    }

    private async Task NextMessagePage()
    {
        if ( _pagedMessages is { HasNextPage: true } )
            await GoToMessagePage( _messagesPage + 1 );
    }

    private async Task OnMessagePageSizeChanged( int size )
    {
        _messagesPageSize = size;
        // Load page 1 to get the new TotalPages, then jump to last page.
        _messagesPage = 1;
        await LoadMessagesAsync();
        if ( _pagedMessages is { TotalPages: > 1 } )
        {
            _messagesPage = _pagedMessages.TotalPages;
            await LoadMessagesAsync();
        }
        StateHasChanged();
    }

    // --- Conversation Pagination ---

    private async Task GoToConversationPage( int page )
    {
        _conversationsPage = page;
        await LoadConversationsAsync();
        StateHasChanged();
    }

    private async Task PreviousConversationPage()
    {
        if ( _conversationsPage > 1 )
            await GoToConversationPage( _conversationsPage - 1 );
    }

    private async Task NextConversationPage()
    {
        if ( _conversations is { HasNextPage: true } )
            await GoToConversationPage( _conversationsPage + 1 );
    }

    private async Task OnConversationPageSizeChanged( int size )
    {
        _conversationsPageSize = size;
        _conversationsPage = 1;
        await GoToConversationPage( 1 );
    }

    // --- User Picker ---

    private void OpenUserPicker() => _showUserPicker = true;

    private void CloseUserPicker() => _showUserPicker = false;

    private async Task HandleUserPicked( ChatUserDto user )
    {
        _showUserPicker = false;
        _isStarting = true;
        StateHasChanged();

        try
        {
            var conversation = await ChatService.StartConversationWithAsync( user.Id );
            if ( conversation is not null )
            {
                _conversationsPage = 1;
                await LoadConversationsAsync();
                await SelectConversation( conversation );
            }
        }
        finally
        {
            _isStarting = false;
            StateHasChanged();
        }
    }

    // --- Messaging ---

    private async Task HandleSendMessage( string message )
    {
        if ( _selectedConversation is null ) return;
        await ChatService.SendMessageAsync( _selectedConversation.Id , message );
    }

    private void HandleBackToList()
    {
        _selectedConversation = null;
    }

    private void HandleMessageReceived( ChatMessageDto message )
    {
        InvokeAsync( () =>
        {
            if ( _selectedConversation is not null && message.ConversationId == _selectedConversation.Id )
            {
                // Only append to the visible list when on the last (newest) page.
                // Use >= so that an empty conversation (TotalPages == 0) also receives the first message.
                if ( _pagedMessages is not null && _messagesPage >= _pagedMessages.TotalPages )
                {
                    _pagedMessages.Items.Add( message );
                    _pagedMessages = _pagedMessages with { TotalCount = _pagedMessages.TotalCount + 1 };
                }

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
                var idx = _conversations.Items.FindIndex( c => c.Id == message.ConversationId );
                if ( idx >= 0 )
                {
                    var old = _conversations.Items [ idx ];
                    _conversations.Items [ idx ] = old with
                    {
                        LastMessageContent = message.Content ,
                        LastMessageAt = message.SentAt
                    };

                    // Move to top.
                    if ( idx > 0 )
                    {
                        var item = _conversations.Items [ idx ];
                        _conversations.Items.RemoveAt( idx );
                        _conversations.Items.Insert( 0 , item );
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
        if ( _conversations is null )
            return;

        var idx = _conversations.Items.FindIndex( c => c.Id == conversationId );
        if ( idx < 0 )
            return;

        var old = _conversations.Items [ idx ];
        var newCount = explicitCount ?? ( old.UnreadCount + 1 );
        _conversations.Items [ idx ] = old with { UnreadCount = newCount };
    }

    // --- Delete Conversation ---

    private void ConfirmDeleteConversation( ChatConversationDto conversation )
    {
        _conversationToDelete = conversation;
    }

    private void CancelDeleteConversation()
    {
        _conversationToDelete = null;
    }

    private async Task ExecuteDeleteConversationAsync()
    {
        if ( _conversationToDelete is null ) return;

        _isDeleting = true;
        StateHasChanged();

        var id = _conversationToDelete.Id;
        var success = await ChatService.DeleteConversationAsync( id );

        if ( success )
        {
            if ( _selectedConversation?.Id == id )
            {
                _selectedConversation = null;
                _pagedMessages = null;
            }

            _conversationsPage = 1;
            await LoadConversationsAsync();
        }

        _conversationToDelete = null;
        _isDeleting = false;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if ( _chatAccess )
        {
            ChatService.OnMessageReceived -= HandleMessageReceived;
            ChatService.OnNotificationReceived -= HandleNotification;
            await ChatService.DisposeAsync();
        }

        GC.SuppressFinalize( this );
    }
}
