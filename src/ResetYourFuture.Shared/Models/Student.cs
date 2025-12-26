namespace ResetYourFuture.Shared.Models;
public sealed record Student(
    int Id,
    string FirstName,
    string LastName,
    int Age,
    string City,
    string Profession
);