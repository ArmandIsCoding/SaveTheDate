namespace SaveTheDate.Domain;

public record ModuleDefinition(ModuleKind Kind, string Name, string Description, string Icon, string DefaultTitle);

public static class ModuleCatalog
{
    public static readonly ModuleDefinition[] All = [
        new(ModuleKind.Header, "Portada", "La primera impresión de tu gran día.", "sparkles", "¡Nos casamos!"),
        new(ModuleKind.Countdown, "Cuenta regresiva", "La emoción de contar los días.", "clock", "Cada vez falta menos"),
        new(ModuleKind.Story, "Nuestra historia", "Un pequeño relato, un gran significado.", "heart", "Así empezó todo"),
        new(ModuleKind.Location, "Lugar y horario", "Ceremonia, fiesta y cómo llegar.", "pin", "Nos encontramos acá"),
        new(ModuleKind.Gallery, "Galería de fotos", "Recuerdos que merecen un lugar especial.", "image", "Nuestros momentos"),
        new(ModuleKind.DressCode, "Dress code", "Una pista para elegir qué ponerse.", "sparkles", "El look de la celebración"),
        new(ModuleKind.Gifts, "Regalos", "Lista de regalos o datos para colaborar.", "gift", "Tu presencia es el mejor regalo"),
        new(ModuleKind.Rsvp, "Confirmar asistencia", "Recibí las respuestas en tu portal.", "users", "¿Nos acompañás?"),
        new(ModuleKind.Music, "Música", "Una playlist o un formulario de canciones.", "music", "La banda sonora de nuestro día"),
        new(ModuleKind.Information, "Información útil", "Alojamiento, traslados y otros detalles.", "info", "Todo lo que necesitás saber"),
        new(ModuleKind.Social, "Redes sociales", "Compartí la emoción antes y después.", "globe", "Sigamos compartiendo")
    ];
    public static ModuleDefinition Get(ModuleKind kind) => All.Single(x => x.Kind == kind);
    public static EventModule Create(ModuleKind kind, int position) => new() { Kind = kind, Position = position, Title = Get(kind).DefaultTitle };
    public static string KindName(EventKind kind) => kind switch { EventKind.Wedding => "Casamiento", EventKind.Birthday => "Cumpleaños", EventKind.Anniversary => "Aniversario", _ => "Celebración" };
    public static bool SafeUrl(string? value) => string.IsNullOrWhiteSpace(value) || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo));
}
