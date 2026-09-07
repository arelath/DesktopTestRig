namespace DesktopTestRig.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DesktopTestRig.Contracts;
using DesktopTestRig.Utility.WpfUtility.Tree;
using NUnit.Framework;
using Forms = System.Windows.Forms;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class VisualTreePropertyExtractorTests
{
	[Test]
	public void WinFormsControlsExposeStableAutomationIdentityAndState()
	{
		using var button = new Forms.Button
		{
			Name = "submitButton",
			AccessibleName = "Submit order",
			Text = "Submit",
			Enabled = false,
		};

		var properties = new VisualTreePropertyExtractor().Extract(button);

		Assert.That(properties[KnownProperties.Name], Is.EqualTo("submitButton"));
		Assert.That(properties[KnownProperties.AutomationName], Is.EqualTo("Submit order"));
		Assert.That(properties[KnownProperties.AutomationId], Is.EqualTo("submitButton"));
		Assert.That(properties[KnownProperties.IsVisible], Is.True);
		Assert.That(properties[KnownProperties.IsEnabled], Is.False);
	}

	[Test]
	public void DefaultPropertySetReadsCommonWpfProperties()
	{
		var button = new Button
		{
			Name = "submitButton",
			Content = "Submit",
			IsEnabled = false,
		};
		AutomationProperties.SetName(button, "Submit order");
		AutomationProperties.SetAutomationId(button, "submit-order");

		var properties = new VisualTreePropertyExtractor().Extract(button);

		Assert.That(VisualTreePropertyExtractor.DefaultPropertyNames, Is.EqualTo(KnownProperties.DefaultVisualTreePropertyNames));
		Assert.That(properties.Keys, Is.EqualTo(VisualTreePropertyExtractor.DefaultPropertyNames));
		Assert.That(properties[KnownProperties.Name], Is.EqualTo("submitButton"));
		Assert.That(properties[KnownProperties.AutomationName], Is.EqualTo("Submit order"));
		Assert.That(properties[KnownProperties.AutomationId], Is.EqualTo("submit-order"));
		Assert.That(properties[KnownProperties.Content], Is.EqualTo("Submit"));
		Assert.That(properties[KnownProperties.IsEnabled], Is.False);
		Assert.That(properties[KnownProperties.Text], Is.TypeOf<PropertyExtractionError>());
	}

	[Test]
	public void ExplicitPropertyListLimitsOutputToRequestedNames()
	{
		var target = new ClrTarget { Name = "ignored", Text = "visible" };

		var properties = new VisualTreePropertyExtractor().Extract(target, new[] { KnownProperties.Text });

		Assert.That(properties.Keys, Is.EqualTo(new[] { KnownProperties.Text }));
		Assert.That(properties[KnownProperties.Text], Is.EqualTo("visible"));
	}

	[Test]
	public void BitmapImageSourceUsesItsUriAsAStablePropertyValue()
	{
		// Register the pack URI parser even when this fixture runs before other WPF tests.
		System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(System.IO.Packaging.PackUriHelper).TypeHandle);
		const string sourceUri = "pack://application:,,,/Assets/toolbar-save.png";
		var source = new BitmapImage();
		source.BeginInit();
		source.CreateOptions = BitmapCreateOptions.DelayCreation;
		source.UriSource = new Uri(sourceUri);
		source.EndInit();
		var image = new Image { Source = source };

		var properties = new VisualTreePropertyExtractor().Extract(image, [KnownProperties.Source]);

		Assert.That(properties[KnownProperties.Source], Is.EqualTo(sourceUri));
	}

	[Test]
	public void DependencyPropertyWithoutClrWrapperCanBeReadByName()
	{
		var target = new DependencyOnlyTarget();
		target.SetValue(DependencyOnlyTarget.CustomValueProperty, "custom");

		var properties = new VisualTreePropertyExtractor().Extract(target, new[] { "CustomValue" });

		Assert.That(properties["CustomValue"], Is.EqualTo("custom"));
	}

	[Test]
	public void MenuItemAutomationIdDoesNotUseStringHeader()
	{
		var target = new MenuItem { Header = "GeneratedChild" };

		var properties = new VisualTreePropertyExtractor().Extract(target, new[] { KnownProperties.AutomationId, KnownProperties.Header });

		Assert.That(properties[KnownProperties.AutomationId], Is.EqualTo(string.Empty));
		Assert.That(properties[KnownProperties.Header], Is.EqualTo("GeneratedChild"));
	}

	[Test]
	public void MissingAndThrowingPropertiesAreStructuredErrors()
	{
		var target = new ThrowingTarget();

		var properties = new VisualTreePropertyExtractor().Extract(target, new[] { "Missing", "Broken" });

		AssertPropertyError(properties["Missing"], "missing-property");
		AssertPropertyError(properties["Broken"], "property-read-failed");
	}

	[Test]
	public void NormalizesArraysAndWpfStructsToJsonFriendlyValues()
	{
		var target = new NormalizationTarget();

		var properties = new VisualTreePropertyExtractor().Extract(target, new[] { "Scores", "Bounds" });

		Assert.That(properties["Scores"], Is.EqualTo(new[] { 1, 2, 3 }));
		Assert.That(properties["Bounds"], Is.EqualTo(new Dictionary<string, object?>
		{
			["X"] = 1d,
			["Y"] = 2d,
			["Width"] = 3d,
			["Height"] = 4d,
		}));
	}

	private static void AssertPropertyError(object? value, string errorCode)
	{
		Assert.That(value, Is.TypeOf<PropertyExtractionError>());
		Assert.That(((PropertyExtractionError)value!).ErrorCode, Is.EqualTo(errorCode));
	}

	[Test]
	public void CachedAccessorsReadLiveValuesAndRetryFailedGetters()
	{
		var extractor = new VisualTreePropertyExtractor();
		var target = new RecoveringTarget();
		AssertPropertyError(extractor.Extract(target, ["Text"])["Text"], "property-read-failed");
		target.Throw = false;
		Assert.That(extractor.Extract(target, ["Text"])["Text"], Is.EqualTo("recovered"));
		Assert.That(extractor.Extract(new ClrTarget { Text = "other instance" }, ["Text"])["Text"], Is.EqualTo("other instance"));
	}

	[Test]
	public void MissingCacheIsSpecificToRuntimeTypeAndDependencyValuesStayLive()
	{
		var extractor = new VisualTreePropertyExtractor();
		AssertPropertyError(extractor.Extract(new object(), ["CustomValue"])["CustomValue"], "missing-property");
		var target = new DependencyOnlyTarget();
		target.SetValue(DependencyOnlyTarget.CustomValueProperty, "first");
		Assert.That(extractor.Extract(target, ["CustomValue"])["CustomValue"], Is.EqualTo("first"));
		target.SetValue(DependencyOnlyTarget.CustomValueProperty, null);
		Assert.That(extractor.Extract(target, ["CustomValue"])["CustomValue"], Is.Null);
	}

	private sealed class RecoveringTarget
	{
		public bool Throw { get; set; } = true;
		public string Text => Throw ? throw new InvalidOperationException("temporary") : "recovered";
	}

	private sealed class ClrTarget
	{
		public string Name { get; set; } = string.Empty;

		public string Text { get; set; } = string.Empty;
	}

	private sealed class DependencyOnlyTarget : DependencyObject
	{
		public static readonly DependencyProperty CustomValueProperty = DependencyProperty.Register(
			"CustomValue",
			typeof(string),
			typeof(DependencyOnlyTarget),
			new PropertyMetadata("default"));
	}

	private sealed class ThrowingTarget
	{
		public string Broken => throw new InvalidOperationException("boom");
	}

	private sealed class NormalizationTarget
	{
		public int[] Scores { get; } = { 1, 2, 3 };

		public Rect Bounds { get; } = new(1, 2, 3, 4);
	}
}
