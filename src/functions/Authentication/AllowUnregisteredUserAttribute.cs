namespace Memoa.Functions.Authentication;

/// <summary>
/// Funzione HTTP autenticata che può essere chiamata anche prima che l'utente applicativo esista
/// (<see cref="UserRegistrationMiddleware"/>): oggi solo <c>GET /me</c>, che lo registra.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowUnregisteredUserAttribute : Attribute;
