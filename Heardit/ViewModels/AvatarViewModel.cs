namespace Heardit.ViewModels
{
    /// <summary>The initials tile that stands in for a profile picture. Size is an .avatar-- modifier.</summary>
    public class AvatarViewModel
    {
        public AvatarViewModel(string? name, string size = "sm")
        {
            Name = name;
            Size = size;
        }

        public string? Name { get; }

        public string Size { get; }

        public string Initials
        {
            get
            {
                var name = (Name ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    return "?";
                }

                var parts = name.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
                }

                return name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
            }
        }
    }
}
