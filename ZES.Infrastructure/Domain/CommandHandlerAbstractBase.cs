using System.Threading.Tasks;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain;

/// <inheritdoc />
public abstract class CommandHandlerAbstractBase<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <inheritdoc />
    public virtual Task Handle(TCommand iCommand, bool trackCompletion) => Handle(iCommand);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="iCommand"></param>
    /// <returns></returns>
    public abstract Task Handle(TCommand iCommand);
    
    /// <inheritdoc />
    public async Task Handle(ICommand command, bool trackCompletion)
    {
        await Handle((TCommand)command, trackCompletion);
    }

    /// <inheritdoc />
    public bool CanHandle(ICommand command) => command is TCommand;
    
    /// <inheritdoc />
    public Task Complete(ICommand command, bool trackCompletion = false) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Uncomplete(ICommand command, bool trackCompletion = false) => Task.CompletedTask;
    
}