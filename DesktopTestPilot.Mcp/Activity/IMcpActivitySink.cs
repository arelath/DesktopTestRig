namespace DesktopTestPilot.Mcp.Activity;

internal interface IMcpActivitySink
{
	void Publish(McpActivityEvent activity);
}
