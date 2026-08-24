namespace DesktopTestRig.Assert;

using System.Linq.Expressions;

internal class ExpressionStringBuilder : ExpressionVisitor
{
	public static string ToString(Expression expression) => expression.ToString();
}
