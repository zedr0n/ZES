using System.Linq;
using Antlr4.StringTemplate;
using ZES.Interfaces.Domain;

namespace ZES.GraphQL
{
    /// <summary>
    /// ST template generator
    /// </summary>
    public class TemplateGenerator
    {
        /// <summary>
        /// Generate the graphql mutation string template
        /// </summary>
        /// <param name="command">Originating command</param>
        /// <returns>Complete mutation</returns>
        public static string GenerateSingleCommand(ICommand command)
        {
            var st = new Template(SingleCommand.Template);
            st.Add(SingleCommand.NameField, command.GetType().Name.ToLowerFirst());

            var constructor = command.GetType().GetConstructors().SingleOrDefault(c => c.GetParameters().Length == 1);
            if (constructor == null)
                return null;

            st.Add(SingleCommand.TargetField, command.Target);
            return st.Render();
        }
        
        private static class SingleCommand
        {
            public static string NameField { get; } = "command";
            public static string TargetField { get; } = "value";
            
            public static string Template => 
                $@"mutation {{ <{NameField}>Ex(command : {{ target : ""<{TargetField}>"" }}) }}";
        }
    }
}