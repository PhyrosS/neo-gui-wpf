﻿namespace Neo.Helpers
{
    /// <summary>
    /// Interface of the DialogHelper that abstracts the usage of Dialog windows in the application.
    /// </summary>
    public interface IDialogHelper
    {
        T ShowDialog<T>(params string[] parameters);
    }
}
