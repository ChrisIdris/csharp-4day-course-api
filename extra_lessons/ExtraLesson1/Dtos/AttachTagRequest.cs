namespace ExtraLesson1.Dtos;

// Request body for POST /api/Todos/{id}/tags. Describes an assignment of an existing
// Tag to the Todo. ColorOverride is optional — when null, the TodoTag inherits the
// Tag's default colour.
public record AttachTagRequest(int TagId, string? ColorOverride);
