namespace BesCrmTest.Data.Services;

// Запитану сутність (задачу, співробітника) не знайдено
public class EntityNotFoundException(string entityName, object id)
    : Exception($"{entityName} with id {id} was not found.");
