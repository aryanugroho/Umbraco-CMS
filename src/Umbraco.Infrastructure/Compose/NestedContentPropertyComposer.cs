﻿using Umbraco.Cms.Core.Composing;
using Umbraco.Core;

namespace Umbraco.Cms.Core.Compose
{
    /// <summary>
    /// A composer for nested content to run a component
    /// </summary>
    public class NestedContentPropertyComposer : ComponentComposer<NestedContentPropertyComponent>, ICoreComposer
    { }
}