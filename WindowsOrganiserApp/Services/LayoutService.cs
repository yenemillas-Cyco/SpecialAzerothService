using Serilog;
using WindowsOrganiserApp.Models;

namespace WindowsOrganiserApp.Services;

public class LayoutService : ILayoutService
{
    private readonly ILogger _logger;

    public LayoutService(ILogger logger)
    {
        _logger = logger;
    }

    public Dictionary<IntPtr, WindowRect> CalculateMainLayout(
        List<WindowInfo> selectedWindows, WindowRect workArea,
        MainSize mainSize, MainPosition mainPosition,
        bool hasLateral, bool hasBandeau)
    {
        var result = new Dictionary<IntPtr, WindowRect>();
        if (selectedWindows.Count == 0) return result;

        var mainWindow = selectedWindows.FirstOrDefault(w => w.IsMainWindow)
                         ?? selectedWindows[0];
        var others = selectedWindows.Where(w => w.Handle != mainWindow.Handle).ToList();

        _logger.Information(
            "Main layout: main={Main}, others={Count}, size={Size}, pos={Pos}, lat={Lat}, band={Band}",
            mainWindow.Title, others.Count, mainSize, mainPosition, hasLateral, hasBandeau);

        if (others.Count == 0)
        {
            result[mainWindow.Handle] = workArea;
            return result;
        }

        if (hasLateral && hasBandeau)
            return CalculateMixteLayout(mainWindow, others, workArea, mainSize, mainPosition, result);
        if (hasBandeau)
            return CalculateBandeauLayout(mainWindow, others, workArea, mainSize, mainPosition, result);
        return CalculateLateralLayout(mainWindow, others, workArea, mainSize, mainPosition, result);
    }

    /// <summary>
    /// Latéral seul : main pleine hauteur, secondaires empilés verticalement.
    /// ┌───────────┬──────┐
    /// │           │ Sec1 │
    /// │   MAIN    ├──────┤
    /// │           │ Sec2 │
    /// │           ├──────┤
    /// │           │ Sec3 │
    /// └───────────┴──────┘
    /// </summary>
    private Dictionary<IntPtr, WindowRect> CalculateLateralLayout(
        WindowInfo mainWindow, List<WindowInfo> others, WindowRect workArea,
        MainSize mainSize, MainPosition mainPosition,
        Dictionary<IntPtr, WindowRect> result)
    {
        var mainLeft = mainPosition is MainPosition.TopLeft or MainPosition.BottomLeft;
        var mainW = (int)(workArea.Width * SizeToRatio(mainSize));
        var secW = workArea.Width - mainW;

        var mainX = mainLeft ? workArea.X : workArea.X + secW;
        var secX = mainLeft ? workArea.X + mainW : workArea.X;

        result[mainWindow.Handle] = new WindowRect(mainX, workArea.Y, mainW, workArea.Height);

        var n = others.Count;
        var cellH = workArea.Height / n;
        for (var i = 0; i < n; i++)
        {
            var y = workArea.Y + i * cellH;
            var h = (i == n - 1) ? workArea.Height - i * cellH : cellH;
            result[others[i].Handle] = new WindowRect(secX, y, secW, h);
        }
        return result;
    }

    /// <summary>
    /// Bandeau seul : main pleine largeur, secondaires en bande horizontale.
    /// ┌──────────────────┐
    /// │      MAIN        │
    /// ├──────┬─────┬─────┤
    /// │ Sec1 │Sec2 │Sec3 │
    /// └──────┴─────┴─────┘
    /// </summary>
    private Dictionary<IntPtr, WindowRect> CalculateBandeauLayout(
        WindowInfo mainWindow, List<WindowInfo> others, WindowRect workArea,
        MainSize mainSize, MainPosition mainPosition,
        Dictionary<IntPtr, WindowRect> result)
    {
        var mainTop = mainPosition is MainPosition.TopLeft or MainPosition.TopRight;
        var mainH = (int)(workArea.Height * SizeToRatio(mainSize));
        var secH = workArea.Height - mainH;

        var mainY = mainTop ? workArea.Y : workArea.Y + secH;
        var secY = mainTop ? workArea.Y + mainH : workArea.Y;

        result[mainWindow.Handle] = new WindowRect(workArea.X, mainY, workArea.Width, mainH);

        var n = others.Count;
        var cellW = workArea.Width / n;
        for (var i = 0; i < n; i++)
        {
            var x = workArea.X + i * cellW;
            var w = (i == n - 1) ? workArea.Width - i * cellW : cellW;
            result[others[i].Handle] = new WindowRect(x, secY, w, secH);
        }
        return result;
    }

    /// <summary>
    /// Mixte (latéral + bandeau) — grille uniforme cols × 3 :
    /// Chaque cellule secondaire a exactement la même taille (cellW × cellH).
    /// Le main occupe mainCols × 2 cellules. Places vides OK.
    ///
    /// Grand (4×3, main 3×2) :           Moyen (3×3, main 2×2) :
    /// ┌────┬────┬────┬────┐             ┌─────┬─────┬─────┐
    /// │              │ S1 │             │          │ S1  │
    /// │    MAIN      ├────┤             │  MAIN    ├─────┤
    /// │    (3×2)     │ S2 │             │  (2×2)   │ S2  │
    /// ├────┬────┬────┼────┤             ├─────┬─────┼─────┤
    /// │ S3 │ S4 │ S5 │ S6 │             │ S3  │ S4  │ S5  │
    /// └────┴────┴────┴────┘             └─────┴─────┴─────┘
    /// </summary>
    private Dictionary<IntPtr, WindowRect> CalculateMixteLayout(
        WindowInfo mainWindow, List<WindowInfo> others, WindowRect workArea,
        MainSize mainSize, MainPosition mainPosition,
        Dictionary<IntPtr, WindowRect> result)
    {
        var (cols, mainCols) = mainSize switch
        {
            MainSize.Grand => (4, 3),
            MainSize.Moyen => (3, 2),
            MainSize.Petit => (4, 2),
            _ => (3, 2)
        };
        const int rows = 3;
        const int mainRows = 2;

        var mainLeft = mainPosition is MainPosition.TopLeft or MainPosition.BottomLeft;
        var mainTop = mainPosition is MainPosition.TopLeft or MainPosition.TopRight;

        var cellW = workArea.Width / cols;
        var cellH = workArea.Height / rows;
        var sideCols = cols - mainCols;

        int X(int col) => workArea.X + col * cellW;
        int Y(int row) => workArea.Y + row * cellH;
        int W(int col) => (col == cols - 1) ? workArea.Width - col * cellW : cellW;
        int H(int row) => (row == rows - 1) ? workArea.Height - row * cellH : cellH;

        var mainColStart = mainLeft ? 0 : sideCols;
        var mainRowStart = mainTop ? 0 : rows - mainRows;
        var sideColStart = mainLeft ? mainCols : 0;
        var sideRowStart = mainRowStart;
        var bandRowStart = mainTop ? mainRows : 0;
        var bandRowEnd = mainTop ? rows : rows - mainRows;

        // Main : mainCols × mainRows cellules
        result[mainWindow.Handle] = new WindowRect(
            X(mainColStart), Y(mainRowStart),
            mainCols * cellW + (mainColStart + mainCols >= cols ? workArea.Width - cols * cellW : 0),
            mainRows * cellH);

        // Slots secondaires — même cellW × cellH pour tous
        var slots = new List<WindowRect>();

        // Latéral : colonne(s) à côté du main
        for (var row = sideRowStart; row < sideRowStart + mainRows; row++)
            for (var col = sideColStart; col < sideColStart + sideCols; col++)
                slots.Add(new WindowRect(X(col), Y(row), W(col), cellH));

        // Bandeau : toutes les colonnes sur les lignes restantes
        for (var row = bandRowStart; row < bandRowEnd; row++)
            for (var col = 0; col < cols; col++)
                slots.Add(new WindowRect(X(col), Y(row), W(col), H(row)));

        for (var i = 0; i < Math.Min(others.Count, slots.Count); i++)
            result[others[i].Handle] = slots[i];

        return result;
    }

    public Dictionary<IntPtr, WindowRect> CalculateSplitLayout(
        List<WindowInfo> selectedWindows, WindowRect workArea)
    {
        var result = new Dictionary<IntPtr, WindowRect>();
        if (selectedWindows.Count == 0) return result;

        _logger.Information("Split layout: {Count} windows", selectedWindows.Count);

        var cols = selectedWindows.Count switch
        {
            1 => 1,
            2 => 2,
            _ => 2
        };
        var rows = (int)Math.Ceiling(selectedWindows.Count / (double)cols);
        var cellW = workArea.Width / cols;
        var cellH = workArea.Height / rows;

        for (var i = 0; i < selectedWindows.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;

            var x = workArea.X + col * cellW;
            var y = workArea.Y + row * cellH;
            var w = (col == cols - 1) ? workArea.Width - col * cellW : cellW;
            var h = (row == rows - 1) ? workArea.Height - row * cellH : cellH;

            result[selectedWindows[i].Handle] = new WindowRect(x, y, w, h);
        }

        return result;
    }

    private static double SizeToRatio(MainSize size) => size switch
    {
        MainSize.Grand => 0.75,
        MainSize.Moyen => 0.667,
        MainSize.Petit => 0.50,
        _ => 0.667
    };
}
