using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PRO.Desktop.Validation;

/// <summary>
/// 可验证的ViewModel基类 - 支持属性级别实时验证
/// </summary>
public abstract class ValidatableViewModelBase : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();
    private readonly Dictionary<string, Func<object?, string?>> _validators = new();

    public bool HasErrors => _errors.Any();
    public bool IsValid => !HasErrors;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return Enumerable.Empty<string>();

        return _errors.GetValueOrDefault(propertyName, new List<string>());
    }

    /// <summary>
    /// 注册属性验证器
    /// </summary>
    protected void RegisterValidator(string propertyName, Func<object?, string?> validator)
    {
        _validators[propertyName] = validator;
    }

    /// <summary>
    /// 验证单个属性
    /// </summary>
    protected void ValidateProperty(object? value, [CallerMemberName] string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        ClearErrors(propertyName);

        if (_validators.TryGetValue(propertyName, out var validator))
        {
            var error = validator(value);
            if (!string.IsNullOrEmpty(error))
            {
                AddError(propertyName, error);
            }
        }
    }

    /// <summary>
    /// 验证所有已注册的属性
    /// </summary>
    protected void ValidateAll()
    {
        foreach (var kvp in _validators)
        {
            // 这里需要获取属性的实际值，简化实现
            ValidateProperty(null, kvp.Key);
        }
    }

    /// <summary>
    /// 添加错误
    /// </summary>
    protected void AddError(string propertyName, string error)
    {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();

        if (!_errors[propertyName].Contains(error))
        {
            _errors[propertyName].Add(error);
            OnErrorsChanged(propertyName);
        }
    }

    /// <summary>
    /// 清除指定属性的错误
    /// </summary>
    protected void ClearErrors(string? propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            _errors.Clear();
        }
        else if (_errors.Remove(propertyName))
        {
            OnErrorsChanged(propertyName);
        }
    }

    private void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(IsValid));
    }
}

/// <summary>
/// 通用验证规则
/// </summary>
public static class ValidationRules
{
    public static string? Required(string? value, string fieldName = "此字段")
    {
        return string.IsNullOrWhiteSpace(value) ? $"{fieldName}不能为空" : null;
    }

    public static string? MaxLength(string? value, int maxLength, string fieldName = "此字段")
    {
        if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
            return $"{fieldName}不能超过{maxLength}个字符";
        return null;
    }

    public static string? MinLength(string? value, int minLength, string fieldName = "此字段")
    {
        if (!string.IsNullOrEmpty(value) && value.Length < minLength)
            return $"{fieldName}不能少于{minLength}个字符";
        return null;
    }

    public static string? Phone(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length != 11 || !value.All(char.IsDigit))
            return "手机号格式不正确，应为11位数字";
        return null;
    }

    public static string? Email(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!value.Contains("@") || !value.Contains("."))
            return "邮箱格式不正确";
        return null;
    }

    public static string? Range(decimal? value, decimal min, decimal max, string fieldName = "值")
    {
        if (!value.HasValue) return null;
        if (value.Value < min || value.Value > max)
            return $"{fieldName}必须在{min}到{max}之间";
        return null;
    }

    public static string? GreaterThan(decimal? value, decimal min, string fieldName = "值")
    {
        if (!value.HasValue) return null;
        if (value.Value <= min)
            return $"{fieldName}必须大于{min}";
        return null;
    }
}

/// <summary>
/// 可验证属性的包装器
/// </summary>
public class ValidatableProperty<T> : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private T? _value;
    private readonly List<string> _errors = new();
    private readonly Func<T?, string?>[] _validators;

    public T? Value
    {
        get => _value;
        set
        {
            SetProperty(ref _value, value);
            Validate();
        }
    }

    public bool HasErrors => _errors.Any();
    public IReadOnlyList<string> Errors => _errors;
    public string? FirstError => _errors.FirstOrDefault();

    public ValidatableProperty(params Func<T?, string?>[] validators)
    {
        _validators = validators;
    }

    public void Validate()
    {
        _errors.Clear();
        foreach (var validator in _validators)
        {
            var error = validator(Value);
            if (!string.IsNullOrEmpty(error))
                _errors.Add(error);
        }
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(Errors));
        OnPropertyChanged(nameof(FirstError));
    }
}
