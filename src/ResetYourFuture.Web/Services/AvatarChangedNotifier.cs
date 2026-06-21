namespace ResetYourFuture.Web.Services;

public sealed class AvatarChangedNotifier
{
    public event Action? AvatarChanged;
    public void Notify() => AvatarChanged?.Invoke();
}
