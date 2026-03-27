using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using ResetYourFuture.Client.Consumers;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Security.Claims;

namespace ResetYourFuture.Client.Pages;

public partial class Chat : IAsyncDisposable
{
    [Inject] private IChatService ChatService { get; set; } = default!;
    [Inject] private ISubscriptionConsumer SubscriptionService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

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
    private string _newMessage = string.Empty;
    private string _currentUserId = string.Empty;
    private bool _isLoadingMessages;
    private bool _isStarting;
    private ElementReference _messageContainer;
    private ElementReference _textareaRef;
    private ElementReference _resizeHandleRef;
    private bool _resizeInitialized;

    // --- User Picker ---
    private bool _showUserPicker;
    private string _userSearchTerm = string.Empty;
    private List<ChatUserDto> _availableUsers = [];
    private bool _isSearching;
    private CancellationTokenSource? _searchCts;

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

        await ChatService.StartAsync();
        await LoadConversationsAsync();
    }

    protected override async Task OnAfterRenderAsync( bool firstRender )
    {
        if ( _selectedConversation is not null && !_resizeInitialized )
        {
            await JS.InvokeVoidAsync( "chatInterop.initTopResize" , _resizeHandleRef , _textareaRef );
            _resizeInitialized = true;
        }
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

    private async Task OnMessagePageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString() , out var size ) )
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

    private async Task OnConversationPageSizeChanged( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString() , out var size ) )
        {
            _conversationsPageSize = size;
            _conversationsPage = 1;
            await GoToConversationPage( 1 );
        }
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

    private async Task SendMessage()
    {
        if ( _selectedConversation is null || string.IsNullOrWhiteSpace( _newMessage ) )
            return;

        await ChatService.SendMessageAsync( _selectedConversation.Id , _newMessage );
        _newMessage = string.Empty;
    }

    private async Task HandleKeyDown( KeyboardEventArgs e )
    {
        if ( e.Key == "Enter" && !e.ShiftKey && !string.IsNullOrWhiteSpace( _newMessage ) )
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
        _conversations.Items [ idx ] = old with
        {
            UnreadCount = newCount
        };
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
                _resizeInitialized = false;
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
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        if ( _chatAccess )
        {
            ChatService.OnMessageReceived -= HandleMessageReceived;
            ChatService.OnNotificationReceived -= HandleNotification;
            await ChatService.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
