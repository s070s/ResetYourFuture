using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ResetYourFuture.Infrastructure.Data;

/// <summary>
/// Model cache key factory that distinguishes an <see cref="ApplicationDbContext"/> built with the
/// at-rest assessment encryption converter (COMP-2) from one built without it. The converter is
/// applied only when a DataProtection provider is injected, so the two produce different models;
/// EF Core's default cache keys the model by context type alone, which would let whichever model is
/// built first serve both — silently encrypting where it shouldn't, or reading ciphertext as
/// plaintext. Including the flag in the key keeps the two models separate whenever a single process
/// happens to construct both (notably the tests, which read the raw column through a converter-less
/// context on the same connection).
/// </summary>
public sealed class EncryptionAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var encryptionEnabled = context is ApplicationDbContext app && app.SensitiveDataEncryptionEnabled;
        return (context.GetType(), designTime, encryptionEnabled);
    }
}
