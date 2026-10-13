using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using System.Collections.ObjectModel;

namespace SmartFoldering.ViewModels;

/// <summary>
/// Creates or edits one destination. Errors appear next to a field once it was edited, or all at once when Save is
/// pressed too early. A test box shows which destination would really take a given file name.
/// </summary>
public sealed partial class DestinationEditorViewModel : ObservableObject
{
    private static readonly char[] InvalidPathChars = ['<', '>', '"', '|', '?', '*'];

    private readonly WatchFolder _folder;
    private readonly string _id;
    private bool _nameTouched, _pathTouched, _autoPath, _settingAutoPath;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _path;
    [ObservableProperty] private string _testName = "";
    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string? _pathError;
    [ObservableProperty] private string? _rulesError;
    [ObservableProperty] private bool _showErrors;
    [ObservableProperty] private bool _isValid;
    [ObservableProperty] private string _testResult = "";
    [ObservableProperty] private bool _testMatches;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DeleteLabel))] private bool _confirmDelete;

    public DestinationEditorViewModel(WatchFolder folder, Destination? existing)
    {
        _folder = folder;
        _id = existing?.Id ?? Guid.NewGuid().ToString("N");
        IsNew = existing is null;
        _autoPath = IsNew;   // a new destination's folder follows its name until the folder is edited
        _name = existing?.Name ?? "";
        _path = existing?.Path ?? "";
        foreach (var rule in existing?.Rules ?? [new Rule(RuleKind.Extension, "")]) Rules.Add(new RuleRowViewModel(this, rule));
        Validate();
    }

    public bool IsNew { get; }
    public string Title => Loc.Instance.Get(IsNew ? "editor.new" : "editor.edit");
    public string Subtitle => Loc.Instance.Format("editor.subtitle", _folder.Name);
    public string WatchedFolderPath => _folder.Path;
    public string DeleteLabel => Loc.Instance.Get(ConfirmDelete ? "editor.delete.confirm" : "editor.delete");
    public bool HasTest => TestName.Trim().Length > 0;
    public ObservableCollection<RuleRowViewModel> Rules { get; } = new();

    public string? ShownNameError => ShowErrors || _nameTouched ? NameError : null;
    public string? ShownPathError => ShowErrors || _pathTouched ? PathError : null;
    public string? ShownRulesError => ShowErrors ? RulesError : null;

    /// <summary>Set by <see cref="Commit"/> or by a confirmed delete.</summary>
    public Destination? Result { get; private set; }
    public bool Deleted { get; private set; }

    partial void OnNameChanged(string value)
    {
        _nameTouched = true;
        if (_autoPath)
        {
            _settingAutoPath = true;
            Path = value.Trim().Length > 0 ? System.IO.Path.Combine(_folder.Path, value.Trim()) : "";
            _settingAutoPath = false;
        }
        Validate();
    }

    partial void OnPathChanged(string value)
    {
        if (!_settingAutoPath)
        {
            _pathTouched = true;
            _autoPath = false;
        }
        Validate();
    }

    partial void OnTestNameChanged(string value) => UpdateTest();

    partial void OnShowErrorsChanged(bool value)
    {
        foreach (var row in Rules) row.Refresh();
        NotifyShownErrors();
    }

    [RelayCommand]
    private void AddRule()
    {
        Rules.Add(new RuleRowViewModel(this, new Rule(RuleKind.Extension, "")));
        Validate();
    }

    internal void Remove(RuleRowViewModel row)
    {
        Rules.Remove(row);
        Validate();
    }

    internal void Validate()
    {
        string name = Name.Trim(), path = Path.Trim();
        NameError = name.Length == 0 ? Loc.Instance.Get("editor.error.name") : null;
        PathError = path.Length == 0 ? Loc.Instance.Get("editor.error.path")
                  : !System.IO.Path.IsPathFullyQualified(path) ? Loc.Instance.Get("editor.error.relative")
                  : path.IndexOfAny(InvalidPathChars) >= 0 || path.IndexOf(':', 2) >= 0 ? Loc.Instance.Get("editor.error.invalid")
                  : MainViewModel.SamePath(path, _folder.Path) ? Loc.Instance.Get("editor.error.same")
                  : null;
        foreach (var row in Rules) row.Validate();
        RulesError = Rules.Count == 0 ? Loc.Instance.Get("editor.error.rules") : null;
        IsValid = NameError is null && PathError is null && RulesError is null && Rules.All(r => r.Error is null);
        NotifyShownErrors();
        UpdateTest();
    }

    private void NotifyShownErrors()
    {
        OnPropertyChanged(nameof(ShownNameError));
        OnPropertyChanged(nameof(ShownPathError));
        OnPropertyChanged(nameof(ShownRulesError));
    }

    private void UpdateTest()
    {
        OnPropertyChanged(nameof(HasTest));
        string file = TestName.Trim();
        if (file.Length == 0) return;

        var rules = Rules.Select(r => r.ToRule()).Where(r => RuleMatcher.Validate(r) is null).ToList();
        if (!rules.Any(r => RuleMatcher.Matches(r, file)))
        {
            TestMatches = false;
            TestResult = Loc.Instance.Get("editor.try.nomatch");
            return;
        }

        // The destination as it would be saved, in its place among the others: does an earlier one take the file?
        var folder = _folder.Clone();
        var draft = new Destination { Id = _id, Name = Name.Trim(), Path = Path.Trim(), Rules = rules };
        int index = folder.Destinations.FindIndex(d => d.Id == _id);
        if (index >= 0) folder.Destinations[index] = draft;
        else folder.Destinations.Add(draft);
        var winner = RuleMatcher.FindDestination(folder, file);

        TestMatches = winner?.Id == _id;
        TestResult = TestMatches ? Loc.Instance.Get("editor.try.match") : Loc.Instance.Format("editor.try.earlier", winner?.Name ?? "");
    }

    /// <summary>Returns true and sets <see cref="Result"/> when everything is valid; otherwise reveals the errors.</summary>
    public bool Commit()
    {
        Validate();
        if (!IsValid)
        {
            ShowErrors = true;
            return false;
        }
        Result = new Destination
        {
            Id = _id,
            Name = Name.Trim(),
            Path = MainViewModel.Normalize(Path),
            Rules = Rules.Select(r => r.ToRule()).ToList()
        };
        return true;
    }

    /// <summary>The first press asks for confirmation, the second one deletes. Returns true when deleted.</summary>
    public bool RequestDelete()
    {
        if (IsNew) return false;
        if (!ConfirmDelete)
        {
            ConfirmDelete = true;
            return false;
        }
        Deleted = true;
        return true;
    }
}

/// <summary>One rule in the editor: a kind, a value and its error.</summary>
public sealed partial class RuleRowViewModel : ObservableObject
{
    private readonly DestinationEditorViewModel _owner;
    private bool _touched;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Hint))] private int _kindIndex;
    [ObservableProperty] private string _value;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShownError))] private string? _error;

    public RuleRowViewModel(DestinationEditorViewModel owner, Rule rule)
    {
        _owner = owner;
        _kindIndex = (int)rule.Kind;
        _value = rule.Value;
        _touched = rule.Value.Length > 0;
    }

    public IReadOnlyList<string> KindNames =>
        [Loc.Instance.Get("rule.kind.extension"), Loc.Instance.Get("rule.kind.name"), Loc.Instance.Get("rule.kind.regex")];

    public string Hint => Loc.Instance.Get(KindIndex switch { 0 => "rule.hint.extension", 1 => "rule.hint.name", _ => "rule.hint.regex" });
    public string? ShownError => _touched || _owner.ShowErrors ? Error : null;

    partial void OnKindIndexChanged(int value) => _owner.Validate();

    partial void OnValueChanged(string value)
    {
        _touched = true;
        _owner.Validate();
    }

    [RelayCommand]
    private void Remove() => _owner.Remove(this);

    public Rule ToRule() => new((RuleKind)Math.Clamp(KindIndex, 0, 2), Value.Trim());

    internal void Validate() => Error = RuleMatcher.Validate(ToRule()) is { } key ? Loc.Instance.Get(key) : null;

    internal void Refresh() => OnPropertyChanged(nameof(ShownError));
}
