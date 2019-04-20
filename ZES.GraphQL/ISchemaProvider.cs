using System;
using HotChocolate;

namespace ZES.GraphQL
{
    public interface ISchemaProvider
    {
        ISchema Generate();
        void SetQuery(Type queryType);
    }
}