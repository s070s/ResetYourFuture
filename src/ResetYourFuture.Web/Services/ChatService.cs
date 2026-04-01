using Microsoft.AspNetCore.SignalR.Client;
using ResetYourFuture.Web.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// Chat service combining REST API calls (history) and SignalR (real-time).
/// Token and hub URL are captured from IHttpContextAccessor during construction
/// (before the Blazor Server circuit transitions away from the HTTP request context).
/// </summary>
public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly string _hubUrl;
    private readonly IAuthService _authService;
    private HubConnection? _hub;

    public event Action<ChatMessageDto>? OnMessageReceived;
    public event Action<ChatNotificationDto>? OnNotificationReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public ChatService( HttpClient http , IAuthService authService , IHttpContextAccessor httpContextAccessor )
    {
        _http = http;
        _authService = authService;

        // Capture hub URL from the current HTTP request before circuit transition.
        var request = httpContextAccessor.HttpContext?.Request;
        var scheme = request?.Scheme ?? "https";
        var host = request?.Host.ToString() ?? "localhost:7090";
        _hubUrl = $"{scheme}://{host}/hubs/chat";
    }

    public async Task StartAsync( System.Security.Claims.ClaimsPrincipal? circuitUser = null )
    {
        if ( _hub is not null )
            return;

        // Use the circuit's cascaded principal when available (HttpContext is null in circuits).
        var token = circuitUser is not null
            ? await _authService.GetTokenAsync( circuitUser )
            : await _authService.GetTokenAsync();

        _hub = new HubConnectionBuilder()
            .WithUrl( _hubUrl , options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>( token );
            } )
            .WithAutomaticReconnect()
            .Build();

        _hub.On<ChatMessageDto>( "ReceiveMessage" , message =>
        {
            OnMessageReceived?.Invoke( message );
        } );

        _hub.On<ChatNotificationDto>( "ChatNotification" , notification =>
        {
            OnNotificationReceived?.Invoke( notification );
        } );

        try
        {
            await _hub.StartAsync();
        }
        catch ( Exception )
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    public async Task StopAsync()
    {
        if ( _hub is not null )
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    public async Task<PagedResult<ChatConversationDto>> GetConversationsAsync( int page = 1 , int pageSize = 10 )
    {
        try
        {
            var response = await _http.GetAsync( $"api/chat/conversations?page={page}&pageSize={pageSize}" );
            if ( response.IsSuccessStatusCode )
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<ChatConversationDto>>()
                       ?? new PagedResult<ChatConversationDto>( [] , 0 , page , pageSize );
            }
        }
        catch ( HttpRequestException ) { }
        return new PagedResult<ChatConversationDto>( [] , 0 , page , pageSize );
    }

    public async Task<PagedResult<ChatMessageDto>> GetMessagesAsync( Guid conversationId , int page = 1 , int pageSize = 20 )
    {
        try
        {
            var response = await _http.GetAsync(
                $"api/chat/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}" );
            if ( response.IsSuccessStatusCode )
            {
                return await response.Content.ReadFromJsonAsync<PagedResult<ChatMessageDto>>()
                       ?? new PagedResult<ChatMessageDto>( [] , 0 , page , pageSize );
            }
        }
        catch ( HttpRequestException ) { }
        return new PagedResult<ChatMessageDto>( [] , 0 , page , pageSize );
    }

    public async Task<ChatConversationDto?> StartConversationWithAsync( string targetUserId , string? initialMessage = null )
    {
        try
        {
            var request = new StartConversationRequest( targetUserId , initialMessage );
            var response = await _http.PostAsJsonAsync( "api/chat/conversations/start" , request );
            if ( response.IsSuccessStatusCode )
            {
                return await response.Content.ReadFromJsonAsync<ChatConversationDto>();
            }
        }
        catch ( HttpRequestException ) { }
        return null;
    }

    public async Task<List<ChatUserDto>> GetAvailableUsersAsync( string? search = null )
    {
        try
        {
            var url = string.IsNullOrWhiteSpace( search )
                ? "api/chat/users"
                : $"api/chat/users?search={Uri.EscapeDataString( search )}";

            var response = await _http.GetAsync( url );
            if ( response.IsSuccessStatusCode )
            {
                return await response.Content.ReadFromJsonAsync<List<ChatUserDto>>() ?? [];
            }
        }
        catch ( HttpRequestException ) { }
        return [];
    }

    public async Task SendMessageAsync( Guid conversationId , string content )
    {
        if ( _hub is not null && IsConnected )
        {
            await _hub.InvokeAsync( "SendMessage" , conversationId , content );
        }
    }

    public async Task MarkAsReadAsync( Guid conversationId )
    {
        if ( _hub is not null && IsConnected )
        {
            await _hub.InvokeAsync( "MarkAsRead" , conversationId );
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var response = await _http.GetAsync( "api/chat/unread-count" );
            if ( response.IsSuccessStatusCode )
            {
                return await response.Content.ReadFromJsonAsync<int>();
            }
        }
        catch ( HttpRequestException ) { }
        return 0;
    }

    public async Task<bool> DeleteConversationAsync( Guid conversationId )
    {
        try
        {
            var response = await _http.DeleteAsync( $"api/chat/conversations/{conversationId}" );
            return response.IsSuccessStatusCode;
        }
        catch ( HttpRequestException ) { }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
