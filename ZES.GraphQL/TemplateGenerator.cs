using System.Linq;
using System.Reflection;
using Antlr4.StringTemplate;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <summary>
    /// ST template generator
    /// </summary>
    public static class TemplateGenerator
    {
        /// <summary>
        /// Generate the graphql mutation string template
        /// </summary>
        /// <param name="command">Originating command</param>
        /// <returns>Complete mutation</returns>
        public static string GenerateMutation(ICommand command)
        {
            var st = new Template(MultiCommand.Template);
            st.Add(MultiCommand.NameField, command.GetType().Name.ToLowerFirst());

            var constructor = command.GetType().GetConstructors().SingleOrDefault(c => c.GetParameters().Length > 0);
            if (constructor == null)
                return null;

            var props = command.GetType().GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

            string BackQuote(PropertyInfo pi)
            {
                if (pi.PropertyType == typeof(string))
                    return '"'.ToString();
                return string.Empty;
            }
            
            var allParams = props
                .Select(p => $"{p.Name.ToLowerFirst()} : {BackQuote(p)}{p.GetValue(command)}{BackQuote(p)}")
                .Aggregate(string.Empty, (current, param) => current + $", {param}");
            
            st.Add(MultiCommand.TargetField, command.Target);
            st.Add(MultiCommand.ParamsField, allParams);
            return st.Render();
        }
        
        /// <summary>
        /// Generate the graphql query string template
        /// </summary>
        /// <param name="query">Originating query</param>
        /// <typeparam name="TResult">Query output type</typeparam>
        /// <returns>Complete mutation</returns>
        public static string GenerateQuery<TResult>(IQuery<TResult> query)
        {
            var st = new Template(MultiQuery.Template);
            st.Add(MultiQuery.NameField, query.GetType().Name.ToLowerFirst());

            var constructor = query.GetType().GetConstructors().SingleOrDefault(c => c.GetParameters().Length > 0);
            var allParams = string.Empty;
            if (constructor != null)
            {
                var paramsProps = query.GetType().GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

                string BackQuote(PropertyInfo pi)
                {
                    if (pi.PropertyType == typeof(string))
                        return '"'.ToString();
                    return string.Empty;
                }
            
                allParams = paramsProps
                    .Select(p => $"{p.Name.ToLowerFirst()} : {BackQuote(p)}{p.GetValue(query)}{BackQuote(p)}")
                    .Aggregate(string.Empty, (current, param) => current + $", {param}");
            }

            var resultProps = typeof(TResult).GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public |
                                                      BindingFlags.Instance);
            var allResults = string.Join(",", resultProps.Select(p => $"{p.Name.ToLowerFirst()}"));
                
            st.Add(MultiQuery.ParamsField, allParams);
            st.Add(MultiQuery.ResultsField, allResults);
            
            return st.Render();
        }

        private static class MultiCommand
        {
            public static string NameField { get; } = "command";
            public static string ParamsField { get; } = "fields";
            public static string TargetField { get; } = "value";
            public static string Template => 
                $@"mutation {{ <{NameField}>Ex( command : {{ target : ""<{TargetField}>""<{ParamsField}> }} ) }}";
        }
        
        private static class MultiQuery
        {
            public static string NameField { get; } = "query";
            public static string ParamsField { get; } = "params";
            public static string ResultsField { get; } = "results";
            public static string Template => 
                $@"{{ <{NameField}>Ex( query : {{ <{ParamsField}> }} ) {{ <{ResultsField}> }} }}";
        }
        
        /*private static class SingleCommand
        {
            public static string NameField { get; } = "command";
            public static string TargetField { get; } = "value";
            
            public static string Template => 
                $@"mutation {{ <{NameField}>Ex(command : {{ target : ""<{TargetField}>"" }}) }}";
        }*/
    }
}