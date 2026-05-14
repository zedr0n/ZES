using System;
using Newtonsoft.Json;
using NodaTime;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    /// <inheritdoc />
    public interface IRetroactiveCommand : ICommand { }

    public static class RetroactiveExtensions
    {
        public static RetroactiveCommand<TCommand> ToRetroactiveCommand<TCommand>(this TCommand command, Time time)
            where TCommand : Command
        {
            var guidOverride = command.Guid;
            // Preserve the externally supplied id on the retroactive wrapper. The wrapped
            // command receives a new id so it does not share the wrapper's command id.
            if(guidOverride != null)
                command.Guid = null;
            return new RetroactiveCommand<TCommand>(command, time) { Guid = guidOverride };    
        }
    }
    
    /// <inheritdoc cref="ZES.Infrastructure.Domain.Command" />
    public class RetroactiveCommand<TCommand> : Command, IRetroactiveCommand
        where TCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetroactiveCommand{TCommand}"/> class.
        /// </summary>
        /// <param name="command">Underlying command</param>
        /// <param name="timestamp">Time at which the command will be actioned</param>
        public RetroactiveCommand(TCommand command, Time timestamp)
        {
            command.RetroactiveId = MessageId;
            Target = command.Target;
            Command = command;
            Timestamp = timestamp;
            Ephemeral = command.Ephemeral;
        }

        /// <inheritdoc />
        public override string Guid
        {
            get => base.Guid;
            set {
                base.Guid = value;
                Command?.RetroactiveId = MessageId;
            }
        }

        /// <inheritdoc />
        [JsonIgnore]
        public override bool Failed
        {
            get => field || (Command?.Failed ?? false); 
            set ;
        }

        /// <summary>
        /// Gets underlying command 
        /// </summary>
        public TCommand Command { get; }
    }
}