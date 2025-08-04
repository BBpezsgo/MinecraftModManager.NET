namespace MMM;

public class ModNotFoundException(string message, Exception? inner = null) : ApplicationException(message, inner);
