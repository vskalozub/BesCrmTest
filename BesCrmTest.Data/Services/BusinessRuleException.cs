namespace BesCrmTest.Data.Services;

// Порушення бізнес-правила (наприклад, призначення задачі неактивному співробітнику)
public class BusinessRuleException(string message) : Exception(message);
