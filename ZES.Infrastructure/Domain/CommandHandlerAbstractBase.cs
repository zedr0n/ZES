using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain;

/// <inheritdoc />
public abstract class CommandHandlerAbstractBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <inheritdoc />
    public abstract Task Handle(TCommand iCommand);
    
    /// <inheritdoc />
    public async Task Handle(ICommand command)
    {
        await Handle((TCommand)command);
    }

    /// <inheritdoc />
    public bool CanHandle(ICommand command) => command is TCommand;
}