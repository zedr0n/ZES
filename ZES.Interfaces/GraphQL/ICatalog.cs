namespace ZES.Interfaces.GraphQL;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a catalog containing a collection of types that implement a specific contract.
/// Provides read-only access to the collection of types.
/// </summary>
/// <typeparam name="TContract">The contract type that the cataloged types implement.</typeparam>
public interface ICatalog<out TContract>
{
    /// <summary>
    /// Gets the collection of types contained in the catalog.
    /// These types implement the contract specified by the generic type parameter.
    /// </summary>
    /// <value>
    /// A read-only collection of types that conform to the contract.
    /// </value>
    IReadOnlyCollection<Type> Types { get; }
}