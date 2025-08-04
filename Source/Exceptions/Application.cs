namespace MMM;

public abstract class ApplicationException(string message, Exception? inner) : Exception(message, inner);