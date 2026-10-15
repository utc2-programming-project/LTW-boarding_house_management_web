using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Web.Components.Shared.Forms;

/// <summary>
/// Đặt trong <c>EditForm</c> để chạy <c>IValidator&lt;TModel&gt;</c> của FluentValidation, dùng cho quy tắc
/// nhiều trường hoặc cần tra cứu. Quy tắc một trường đơn giản vẫn dùng DataAnnotations; hai bộ kiểm tra
/// có thể đứng cạnh nhau trong cùng form. Chỉ hỗ trợ model phẳng (thuộc tính trực tiếp của model).
/// </summary>
public sealed class FluentValidationValidator : ComponentBase, IDisposable
{
    private ValidationMessageStore? _messages;
    private EditContext? _editContext;

    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    /// <summary>Validator cụ thể. Bỏ trống thì lấy <c>IValidator&lt;TModel&gt;</c> đã đăng ký trong DI.</summary>
    [Parameter]
    public IValidator? Validator { get; set; }

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    protected override void OnInitialized()
    {
        _editContext = CurrentEditContext
            ?? throw new InvalidOperationException(
                $"{nameof(FluentValidationValidator)} phải nằm trong một EditForm.");

        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnValidationRequested += OnValidationRequested;
        _editContext.OnFieldChanged += OnFieldChanged;
    }

    private IValidator? ResolveValidator()
    {
        if (Validator is not null) return Validator;

        var modelType = _editContext!.Model.GetType();
        return Services.GetService(typeof(IValidator<>).MakeGenericType(modelType)) as IValidator;
    }

    private void OnValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _messages!.Clear();

        var validator = ResolveValidator();
        if (validator is null) return;

        var result = validator.Validate(new ValidationContext<object>(_editContext!.Model));
        foreach (var error in result.Errors)
        {
            _messages.Add(_editContext.Field(error.PropertyName), error.ErrorMessage);
        }

        _editContext.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        var validator = ResolveValidator();
        if (validator is null) return;

        var field = e.FieldIdentifier;
        _messages!.Clear(field);

        var context = new ValidationContext<object>(
            field.Model,
            new PropertyChain(),
            new MemberNameValidatorSelector([field.FieldName]));

        var result = validator.Validate(context);
        foreach (var error in result.Errors)
        {
            _messages.Add(field, error.ErrorMessage);
        }

        _editContext!.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (_editContext is not null)
        {
            _editContext.OnValidationRequested -= OnValidationRequested;
            _editContext.OnFieldChanged -= OnFieldChanged;
        }

        _messages?.Clear();
    }
}
