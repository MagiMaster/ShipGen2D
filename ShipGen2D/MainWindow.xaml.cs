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

        public void AddRectRoom(int x1, int y1, int x2, int y2) {
            Polygon p = new Polygon();

            if (x2 < x1) {
                int temp = x1;
                x1 = x2;
                x2 = temp;
            }
            if (y2 < y1) {
                int temp = y1;
                y1 = y2;
                y2 = temp;
            }

            p.Add(new IntPoint(x1, y1));
            p.Add(new IntPoint(x2, y1));
            p.Add(new IntPoint(x2, y2));
            p.Add(new IntPoint(x1, y2));

            rooms.Add(p);
        }

        public Polygon MirrorY(Polygon p, int my) {
            Polygon o = new Polygon();
            for (int i = p.Count - 1; i >= 0; --i)
                o.Add(new IntPoint(p[i].X, 2 * my - p[i].Y));
            return o;
        }

        public MainWindow() {
            InitializeComponent();

            c = new Clipper();

            rooms = new Polygons();
            AddRectRoom(50, 50, 150, 100);
            AddRectRoom(100, 100, 200, 150);
            AddRectRoom(50, 150, 150, 200);

            decoration = new Polygons();
            Polygon p = new Polygon();
            p.Add(new IntPoint(30, 40));
            p.Add(new IntPoint(175, 80));
            p.Add(new IntPoint(50, 110));
            decoration.Add(p);
            decoration.Add(MirrorY(p, 125));

            redraw();
        }

        public void redraw() {
            canvas.Children.Clear();

            c.Clear();
            Polygons outline = new Polygons();

            c.AddPolygons(rooms, PolyType.ptSubject);
            c.AddPolygons(decoration, PolyType.ptSubject);
            c.Execute(ClipType.ctUnion, outline, PolyFillType.pftNonZero, PolyFillType.pftNonZero);
            outline = Clipper.OffsetPolygons(outline, 10, JoinType.jtMiter);

            // Bevel inside corners of the outline
            foreach (Polygon p in outline) {
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
                canvas.Children.Add(ToSystemPolygon(p, Brushes.Red));
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
            redraw();
        }
    }
}
