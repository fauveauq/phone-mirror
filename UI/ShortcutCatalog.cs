namespace PhoneMirror.UI;

/// <summary>Un raccourci affiché dans l'aide : touches ou geste, et son effet.</summary>
public sealed record Shortcut(string Keys, string Effect);

/// <summary>Groupe de raccourcis (souris, clavier…).</summary>
public sealed record ShortcutGroup(string Title, IReadOnlyList<Shortcut> Items);

/// <summary>
/// Liste des raccourcis affichée par la fenêtre d'aide. À tenir à jour avec
/// Input/MouseForwarder.cs, Input/KeyboardForwarder.cs, Input/PhoneShortcuts.cs et MainWindow.
/// </summary>
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutGroup> Groups { get; } =
    [
        new("Souris",
        [
            new("Clic gauche", "Toucher"),
            new("Clic gauche maintenu + glisser", "Faire défiler, balayer, déplacer"),
            new("Molette", "Défilement vertical"),
            new("Maj + molette", "Défilement horizontal"),
            new("Clic droit", "Retour (rallume l'écran s'il est éteint)"),
            new("Clic de la molette", "Accueil"),
            new("Boutons latéraux", "Applications récentes / notifications"),
        ]),
        new("Clavier",
        [
            new("Frappe", "Texte sur le téléphone, accents compris"),
            new("Ctrl+V", "Coller le texte copié sur le PC"),
            new("Ctrl+C / Ctrl+X", "Copier / couper la sélection du téléphone vers le PC"),
            new("Ctrl+A, Ctrl+Z…", "Transmis au téléphone (tout sélectionner, annuler…)"),
        ]),
        new("Téléphone",
        [
            new("Alt+H", "Accueil"),
            new("Alt+B", "Retour"),
            new("Alt+S", "Applications récentes"),
            new("Alt+M", "Menu"),
            new("Alt+↑ / Alt+↓", "Volume + / −"),
            new("Alt+P", "Bouton marche/arrêt"),
            new("Alt+N / Alt+Maj+N", "Ouvrir / fermer les notifications"),
            new("Alt+R", "Faire pivoter l'écran"),
            new("Alt+O / Alt+Maj+O", "Éteindre / rallumer l'écran du téléphone"),
        ]),
        new("Phone Mirror",
        [
            new("F1", "Cette aide"),
            new("F2", "Statistiques"),
            new("F3", "Réglages"),
            new("F11", "Plein écran"),
            new("Glisser un fichier", "APK : installé ; autre fichier : copié dans Téléchargements"),
        ]),
    ];
}
