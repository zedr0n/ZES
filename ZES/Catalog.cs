using ZES.Interfaces.GraphQL;

namespace ZES;

using System;
using System.Collections.Generic;
using System.Linq;

/// <inheritdoc />
public sealed class TypeCatalog<TContract>(IEnumerable<Type> types) : ICatalog<TContract>
{
    /// <inheritdoc />
    public IReadOnlyCollection<Type> Types { get; } = (types ?? throw new ArgumentNullException(nameof(types))).ToArray();
}