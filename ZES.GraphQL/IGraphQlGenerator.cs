using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <summary>
    /// Generate graphql syntax from internal CQRS objects
    /// </summary>
    public interface IGraphQlGenerator
    {
        /// <summary>
        /// Generate the graphql mutation string template
        /// </summary>
        /// <param name="command">Originating command</param>
        /// <returns>Complete mutation</returns>
        string Mutation(ICommand command);

        /// <summary>
        /// Generate the graphql query string template
        /// </summary>
        /// <param name="query">Originating query</param>
        /// <typeparam name="TResult">Query output type</typeparam>
        /// <returns>Complete mutation</returns>
        string Query<TResult>(IQuery<TResult> query);
    }
}