namespace DesktopTestPilot.Interop.Expressions;

using System.Linq;
using System.Linq.Expressions;
using DesktopTestPilot.Interop.Expressions.Visitors;
using Serialize.Linq.Factories;
using Serialize.Linq.Interfaces;
using Serialize.Linq.Serializers;
using SerializeJsonSerializer = Serialize.Linq.Serializers.JsonSerializer;

internal sealed class DesktopTestPilotSerializeLinqSerializer : ExpressionSerializer
{
	private static readonly FactorySettings Settings = new()
	{
		AllowPrivateFieldAccess = true,
	};

	public DesktopTestPilotSerializeLinqSerializer()
		: base(new SerializeJsonSerializer(), Settings)
	{
	}

	protected override INodeFactory CreateFactory(Expression expression, FactorySettings factorySettings)
	{
		var expectedTypes = ExpressionExpectedTypeCollector.Collect(expression);
		if (expression is LambdaExpression lambda)
			expectedTypes.AddRange(lambda.Parameters.Select(static parameter => parameter.Type));

		return new DefaultNodeFactory(expectedTypes, factorySettings);
	}
}
