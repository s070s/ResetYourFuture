using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using ResetYourFuture.Application.DTOs;
using ResetYourFuture.Domain.Enums;
using ResetYourFuture.Shared.Resources;
using ResetYourFuture.Web.Interfaces;

namespace ResetYourFuture.Web.Shared.Components.Chat;

public partial class MessagePane : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ICallService CallService { get; set; } = default!;

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

    private ElementReference _messageContainer;
    private ElementReference _textareaRef;
    private ElementReference _resizeHandleRef;
    private bool _resizeInitialized;
    private bool _clearInputPending;
    private Guid? _previousConversationId;

    protected override void OnInitialized()
    {
        CallService.StateChanged += HandleCallStateChanged;
    }

    private void HandleCallStateChanged() => InvokeAsync(StateHasChanged);

    private async Task StartVideoCall()
    {
        if (Conversation is null) return;
        await CallService.StartCallAsync([Conversation.OtherUserId], Conversation.Id);
    }

    private static string GetCallEventText(ChatMessageDto msg) => msg.CallEvent switch
    {
        CallEventKind.Started => CallRes.CallStarted,
        CallEventKind.Missed => CallRes.MissedCall,
        CallEventKind.Ended => string.Format(CallRes.CallEndedWithDuration, FormatDuration(msg.CallDurationSeconds)),
        _ => string.Empty
    };

    private static string FormatDuration(int? seconds)
    {
        var span = TimeSpan.FromSeconds(seconds ?? 0);
        return $"{(int)span.TotalMinutes}:{span.Seconds:D2}";
    }

    public void Dispose()
    {
        CallService.StateChanged -= HandleCallStateChanged;
    }

    protected override void OnParametersSet()
    {
        if (Conversation?.Id != _previousConversationId)
        {
            _previousConversationId = Conversation?.Id;
            _clearInputPending = true;
            _resizeInitialized = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (Conversation is not null && !_resizeInitialized)
        {
            await JS.InvokeVoidAsync("chatInterop.initTopResize", _resizeHandleRef, _textareaRef);
            _resizeInitialized = true;
        }

        // The textarea is uncontrolled (PERF-6), so clear it here when switching conversations.
        if (_clearInputPending)
        {
            _clearInputPending = false;
            await JS.InvokeVoidAsync("inputInterop.clear", _textareaRef);
        }
    }

    private async Task SendMessage()
    {
        // Read the current text from the DOM only at send time — keystrokes never round-trip.
        var msg = await JS.InvokeAsync<string>("inputInterop.read", _textareaRef);
        if (string.IsNullOrWhiteSpace(msg)) return;
        await JS.InvokeVoidAsync("inputInterop.clear", _textareaRef);
        await OnSendMessage.InvokeAsync(msg.Trim());
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessage();
    }

    private async Task HandlePageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var size))
            await OnPageSizeChanged.InvokeAsync(size);
    }
}
