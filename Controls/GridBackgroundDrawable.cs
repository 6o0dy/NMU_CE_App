namespace NMU_CE_App.Controls;

public class GridBackgroundDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var gridColor = Color.FromArgb("#0D00F2FF");
        float gridSize = 40f;
        float width = dirtyRect.Width;
        float height = dirtyRect.Height;

        if (width <= 0 || height <= 0) return;

        canvas.StrokeColor = gridColor;
        canvas.StrokeSize = 1;

        for (float x = 0; x <= width; x += gridSize)
            canvas.DrawLine(x, 0, x, height);

        for (float y = 0; y <= height; y += gridSize)
            canvas.DrawLine(0, y, width, y);
    }
}
