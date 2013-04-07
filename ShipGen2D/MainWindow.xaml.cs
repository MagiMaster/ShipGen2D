using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using ClipperLib;

namespace ShipGen2D {
    using Polygon = List<IntPoint>;
    using Polygons = List<List<IntPoint>>;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public Polygons rooms, decoration;
        public Clipper c;
        public Random rnd;

        public static System.Windows.Shapes.Polygon ToSystemPolygon(Polygon p) {
            return ToSystemPolygon(p, Brushes.White, Brushes.Transparent);
        }

        public static System.Windows.Shapes.Polygon ToSystemPolygon(Polygon p, Brush stroke) {
            return ToSystemPolygon(p, stroke, Brushes.Transparent);
        }

        public static System.Windows.Shapes.Polygon ToSystemPolygon(Polygon p, Brush stroke, Brush fill) {
            System.Windows.Shapes.Polygon o = new System.Windows.Shapes.Polygon();
            o.StrokeThickness = 2;

            foreach (IntPoint i in p)
                o.Points.Add(new Point(i.X + 0.5 * (o.StrokeThickness % 2), i.Y + 0.5 * (o.StrokeThickness % 2)));
            o.Stroke = stroke;
            o.Fill = fill;
            return o;
        }

        public void AddRectRoom(int x, int y, int w, int h) {
            Polygon p = new Polygon();

            p.Add(new IntPoint(x, y));
            p.Add(new IntPoint(x + w, y));
            p.Add(new IntPoint(x + w, y + h));
            p.Add(new IntPoint(x, y + h));

            rooms.Add(p);
        }

        public Polygon MirrorY(Polygon p, int my) {
            Polygon o = new Polygon();
            for (int i = p.Count - 1; i >= 0; --i)
                o.Add(new IntPoint(p[i].X, 2 * my - p[i].Y));
            return o;
        }

        public void generateRooms(int width, int height, bool sym = true) {
            rooms.Clear();
            decoration.Clear();

            int[,] grid = new int[width, height];

            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                    grid[x, y] = 0;

            int scale = (int)Math.Min(canvas.RenderSize.Width / (width + 6), canvas.RenderSize.Height / (height + 6));
            double mid = canvas.RenderSize.Height / 2;
            int offsetX = (int)((canvas.RenderSize.Width - scale * width) / 2);
            int offsetY = (int)((canvas.RenderSize.Height - scale * height) / 2);

            int hh = height;
            if (sym)
                hh = (height + 1) / 2;

            for(int y = 0; y < hh; ++y)
                for (int x = 0; x < width; ++x) {
                    double u = 1.0 - Math.Abs(2.0 * y - height) / height;
                    double v = width*(0.2 + 0.3 * u);
                    double w = (x < v ? x / v : (width - x) / (width - v));
                    double p = (0.05 + 0.95 * w) * (0.25 + 0.75 * u);

                    if (rnd.NextDouble() < p) {
                        grid[x, y] = -1;
                        if (sym) grid[x, height - y - 1] = -1;
                    }
                }

            for(int i = 0; i < 2; ++i)
            {
                int[,] temp = new int[width, height];

                for(int y = 0; y < height; ++y)
                    for (int x = 0; x < width; ++x) {
                        int count = 0;

                        for (int v = -1; v <= 1; ++v)
                            for (int u = -1; u <= 1; ++u) {
                                if (u == 0 && v == 0)
                                    continue;

                                int dx = x + u;
                                int dy = y + v;

                                if (dx < 0 || dx >= width || dy < 0 || dy >= height)
                                    continue;

                                count += (grid[dx, dy] == -1 ? 1 : 0);
                            }

                        temp[x, y] = count;
                    }

                for(int y = 0; y < height; ++y)
                    for (int x = 0; x < width; ++x) {
                        if (temp[x, y] == 2 || temp[x, y] == 5 || temp[x, y] == 7)
                            grid[x, y] = -1;
                        else if (temp[x, y] == 3 && grid[x, y] == 0)
                            grid[x, y] = -1;
                        else if ((temp[x, y] == 1 || temp[x, y] == 6 || temp[x, y] == 8) && grid[x, y] == -1)
                            grid[x, y] = -1;
                        else if (grid[x, y] == -1)
                            grid[x, y] = -2;
                        else
                            grid[x, y] = 0;
                    }
            }

            //Temporary
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                    if (grid[x, y] != 0)
                        AddRectRoom(offsetX + scale * x, offsetY + scale * y, scale, scale);
        }

        public void generateDecorations() {
        }

        public MainWindow() {
            InitializeComponent();

            rnd = new Random();

            c = new Clipper();

            rooms = new Polygons();
            decoration = new Polygons();
        }

        public void redraw() {
            canvas.Children.Clear();

            if (rooms.Count == 0 && decoration.Count == 0)
                return;

            c.Clear();
            Polygons outline = new Polygons();

            c.AddPolygons(rooms, PolyType.ptSubject);
            c.AddPolygons(decoration, PolyType.ptSubject);
            c.Execute(ClipType.ctUnion, outline, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            outline = Clipper.OffsetPolygons(outline, 10, JoinType.jtMiter);

            // Bevel inside corners of the outline
            foreach (Polygon p in outline) {
                // Skip holes
                if (!Clipper.Orientation(p))
                    continue;

                // Find the inside corners
                List<bool> corners = new List<bool>();
                for (int j = 0; j < p.Count; ++j) {
                    int i = (j + p.Count - 1) % p.Count;
                    int k = (j + 1) % p.Count;

                    IntPoint e1 = new IntPoint(p[j].X - p[i].X, p[j].Y - p[i].Y);
                    IntPoint e2 = new IntPoint(p[j].X - p[k].X, p[j].Y - p[k].Y);

                    int a = (int)(e1.X * e2.Y - e2.X * e1.Y);

                    corners.Add(a > 0);
                }

                // Bevel them to the midpoints of the original lines
                for (int j = 0; j < p.Count; ++j) {
                    int i = (j + p.Count - 1) % p.Count;
                    int k = (j + 1) % p.Count;

                    if (corners[j]) {
                        if(!corners[i]) {
                            corners.Insert(j, true);
                            p.Insert(j, new IntPoint((p[i].X + p[j].X)/2, (p[i].Y + p[j].Y)/2));

                            ++j; ++k;
                        }

                        p[j] = new IntPoint((p[j].X + p[k].X) / 2, (p[j].Y + p[k].Y) / 2);
                    }
                }
            }

            foreach (Polygon p in rooms)
                canvas.Children.Add(ToSystemPolygon(p));

            foreach (Polygon p in outline) {
                canvas.Children.Add(ToSystemPolygon(p, Brushes.Yellow));
            }

            UpdateLayout();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e) {
            //canvas.Measure(new Size(Width, Height));
            RenderTargetBitmap bmp = new RenderTargetBitmap((int)canvas.RenderSize.Width, (int)canvas.RenderSize.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(canvas);
            PngBitmapEncoder png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bmp));
            using (var file = File.Create("out.png")) {
                png.Save(file);
            }
        }

        private void quitButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void rerollButton_Click(object sender, RoutedEventArgs e) {
            int w, h;

            if (!int.TryParse(widthBox.Text, out w) || !int.TryParse(heightBox.Text, out h))
                MessageBox.Show("Please enter a valid width and height", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            else {
                generateRooms(int.Parse(widthBox.Text), int.Parse(heightBox.Text), symBox.IsChecked.Value);
                generateDecorations();
                redraw();
            }
        }
    }
}
