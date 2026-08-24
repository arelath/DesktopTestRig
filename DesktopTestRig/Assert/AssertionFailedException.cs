namespace DesktopTestRig.Assert;

using System;

public class AssertionFailedException : Exception
{
	public AssertionFailedException(string message)
		: base(message)
	{
	}
}
