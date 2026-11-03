using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace Web.Components.Shared.Forms;

public static class FieldStateExtensions
{
    public static bool HasError<T>(this EditContext context, Expression<Func<T>> accessor) =>
        context.GetValidationMessages(FieldIdentifier.Create(accessor)).Any();
}
