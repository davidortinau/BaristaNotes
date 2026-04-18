namespace BaristaNotes.Core.Services.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityType, int id) 
        : base($"{entityType} with ID {id} not found") { }
}

public class ValidationException : Exception
{
    public Dictionary<string, List<string>> Errors { get; }
    
    public ValidationException(Dictionary<string, List<string>> errors) 
        : base("Validation failed")
    {
        Errors = errors;
    }
}
