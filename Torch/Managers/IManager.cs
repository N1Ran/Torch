﻿namespace Torch.Managers
{
    /// <summary>
    ///     Base interface for Torch managers.
    /// </summary>
    public interface IManager
    {
        /// <summary>
        ///     Attaches the manager to the session. Called once this manager's dependencies have been attached.
        /// </summary>
        void Attach();

        /// <summary>
        ///     Detaches the manager from the session.  Called before this manager's dependencies are detached.
        /// </summary>
        void Detach();
    }
}