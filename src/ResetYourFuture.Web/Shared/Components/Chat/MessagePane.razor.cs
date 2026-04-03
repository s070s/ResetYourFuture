using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using ResetYourFuture.Shared.DTOs;

namespace ResetYourFuture.Web.Shared.Components.Chat;

public partial class MessagePane
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public ChatConversationDto? Conversation { get; set; }
    [Parameter, EditorRequired] public PagedResult<ChatMessageDto>? Messages { get; set; }
    [Parameter, EditorRequired] public string CurrentUserId { get; set; } = string.Empty;
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsConnected { get; set; }
    [Parameter, EditorRequired] public int PageSize { get; set; }
    [Parameter, EditorRequired] public int[] PageSizeOptions { get; set; } = [];
    [Parameter, EditorRequired] public EventCallback<string> OnSendMessage { get; set; }
    [Parameter, EditorRequired] public EventCallback<int> OnPageSizeChanged { get; set; }
    [Parameter, EditorRequired] public EventCallback OnPreviousPage { get; set; }
    [Parameter, EditorRequired] public EventCallback OnNextPage { get; set; }
    [Parameter] public EventCallback OnBackToList { get; set; }

    private string _newMessage = string.Empty;
    private ElementReference _messageContainer;
    private ElementReference _textareaRef;
    private ElementReference _resizeHandleRef;
    private bool _resizeInitialized;
    private Guid? _previousConversationId;

    protected override void OnParametersSet()
    {
        if ( Conversation?.Id != _previousConversationId )
        {
            _previousConversationId = Conversation?.Id;
            _newMessage = string.Empty;
            _resizeInitialized = false;
        }
    }

    protected override async Task OnAfterRenderAsync( bool firstRender )
    {
        if ( Conversation is not null && !_resizeInitialized )
        {
            await JS.InvokeVoidAsync( "chatInterop.initTopResize" , _resizeHandleRef , _textareaRef );
            _resizeInitialized = true;
        }
    }

    private async Task SendMessage()
    {
        if ( string.IsNullOrWhiteSpace( _newMessage ) ) return;
        var msg = _newMessage;
        _newMessage = string.Empty;
        await OnSendMessage.InvokeAsync( msg );
    }

    private async Task HandleKeyDown( KeyboardEventArgs e )
    {
        if ( e.Key == "Enter" && !e.ShiftKey && !string.IsNullOrWhiteSpace( _newMessage ) )
            await SendMessage();
    }

    private async Task HandlePageSizeChange( ChangeEventArgs e )
    {
        if ( int.TryParse( e.Value?.ToString() , out var size ) )
            await OnPageSizeChanged.InvokeAsync( size );
    }
}
