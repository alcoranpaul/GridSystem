﻿using Flax.Build;

public class GridSystemEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        // Reference the modules for editor
        Modules.Add("GridSystem");
        Modules.Add("GridSystemEditor");
    }
}
