namespace ConsumerService.Processing;

public abstract class MessageProcessingException(string message) : Exception(message);

public sealed class PermanentMessageException(string message) : MessageProcessingException(message);

public sealed class TransientMessageException(string message, Exception? innerException = null)
    : MessageProcessingException(message)
{
    public Exception? RootCause { get; } = innerException;
}

