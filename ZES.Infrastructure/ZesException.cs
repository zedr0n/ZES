using System;

namespace ZES.Infrastructure;

/// <summary>
/// Represents a custom exception specific to the ZES infrastructure.
/// </summary>
/// <remarks>
/// The <c>ZesException</c> class is designed to optionally mark exceptions as ignorable.
/// This can be useful for scenarios where specific exceptions need to be logged or handled
/// without propagating them further in the execution flow.
/// </remarks>
/// <param name="ignore">
/// A boolean value indicating whether the exception is ignorable.
/// If set to <c>true</c>, the exception can be marked for special handling
/// to prevent interruption of the application flow.
/// </param>
public class ZesException(bool ignore = false) : Exception
{
    /// <summary>
    /// Gets a value indicating whether the associated exception is marked as ignorable.
    /// </summary>
    /// <remarks>
    /// This property represents the status of an exception being ignored during the execution flow.
    /// If <c>true</c>, the exception is considered ignorable and can be handled or logged without
    /// halting the application's operation. This is particularly useful in scenarios where specific
    /// exceptions are expected and do not require further propagation.
    /// </remarks>
    public bool Ignore => ignore;
}