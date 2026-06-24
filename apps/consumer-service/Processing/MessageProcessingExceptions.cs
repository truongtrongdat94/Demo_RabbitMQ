namespace ConsumerService.Processing;

public abstract class MessageProcessingException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class PermanentMessageException(string message, Exception? innerException = null)
    : MessageProcessingException(message, innerException);

public sealed class TransientMessageException(string message, Exception? innerException = null)
    : MessageProcessingException(message, innerException);
