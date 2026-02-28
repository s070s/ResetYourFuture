using Blazored.LocalStorage;
using Microsoft.AspNetCore.SignalR.Client;
using ResetYourFuture.Client.Interfaces;
using ResetYourFuture.Shared.DTOs;
using System.Net.Http.Json;

namespace ResetYourFuture.Client.Services;

/// <summary>
/// Chat service combining REST API calls (history) and SignalR (real-time).
/// </summary>
public class ChatService : IChatService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private readonly string _hubUrl;
    private HubConnection? _hub;

    public event Action<ChatMessageDto>? OnMessageReceived;
    public event Action<ChatNotificationDto>? OnNotificationReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public ChatService( HttpClient http , ILocalStorageService localStorage , IConfiguration config )
    {
        _http = http;
        _localStorage = localStorage;
        var apiBase = config [ "ApiBaseUrl" ] ?? "https://localhost:7003";
        _hubUrl = $"{apiBase}/hubs/chat";
    }

    public async Task StartAsync()
    {
        if ( _hub is not null )
            return;

        var token = await _localStorage.GetItemAsStringAsync( "authToken" );

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

        await _hub.StartAsync();
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

    public async Task<List<ChatConversationDto>> GetConversationsAsync()
    {
        var response = await _http.GetAsync( "api/chat/conversations" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<List<ChatConversationDto>>() ?? [];
        }
        return [];
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync( Guid conversationId , int skip = 0 , int take = 50 )
    {
        var response = await _http.GetAsync(
            $"api/chat/conversations/{conversationId}/messages?skip={skip}&take={take}" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<List<ChatMessageDto>>() ?? [];
        }
        return [];
    }

    public async Task<ChatConversationDto?> StartConversationWithAsync( string targetUserId , string? initialMessage = null )
    {
        var request = new StartConversationRequest( targetUserId , initialMessage );
        var response = await _http.PostAsJsonAsync( "api/chat/conversations/start" , request );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<ChatConversationDto>();
        }
        return null;
    }

    public async Task<List<ChatUserDto>> GetAvailableUsersAsync( string? search = null )
    {
        var url = string.IsNullOrWhiteSpace( search )
            ? "api/chat/users"
            : $"api/chat/users?search={Uri.EscapeDataString( search )}";

        var response = await _http.GetAsync( url );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<List<ChatUserDto>>() ?? [];
        }
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
        var response = await _http.GetAsync( "api/chat/unread-count" );
        if ( response.IsSuccessStatusCode )
        {
            return await response.Content.ReadFromJsonAsync<int>();
        }
        return 0;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
