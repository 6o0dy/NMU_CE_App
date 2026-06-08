namespace NMU_CE_App.Services;

public static class NavHelper
{
    private static readonly Dictionary<Page, Page> _parents = new();

    public static void Go(Page from, Page to)
    {
        _parents[to] = from;
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = to;
    }

    public static void Back(Page from)
    {
        if (_parents.TryGetValue(from, out var parent))
        {
            _parents.Remove(from);
            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = parent;
        }
    }

    public static void BackToRoot(Page from)
    {
        while (true)
        {
            if (!_parents.TryGetValue(from, out var parent))
                break;
            var grandparent = _parents.ContainsKey(parent) ? parent : null;
            _parents.Remove(from);
            if (grandparent == null || !_parents.ContainsKey(grandparent))
            {
                if (Application.Current?.Windows.Count > 0)
                    Application.Current.Windows[0].Page = parent;
                return;
            }
            from = parent;
        }
    }

    public static void ReplaceAndGo(Page from, Page to)
    {
        if (_parents.TryGetValue(from, out var parent))
        {
            _parents[to] = parent;
            _parents.Remove(from);
        }
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = to;
    }
}
