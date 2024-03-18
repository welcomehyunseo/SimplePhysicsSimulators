
using SFML.System;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;

namespace SimpleTwoBallsPlainCollisionSimulator
{

    abstract class Window
    {

        private readonly SFML.Graphics.RenderWindow _RenderWindow;

        private bool _run = false;

        private readonly uint _Width, _Height;
        public uint Width { get { return _Width; } }
        public uint Height { get { return _Height; } }

        private readonly float _scale;

        public readonly float MaxX, MaxY;

        public Window(uint width, uint height, float scale = 1)
        {
            _RenderWindow = 
                new SFML.Graphics.RenderWindow(
                    new SFML.Window.VideoMode(width, height), 
                    "SimpleTwoBallsLinearCollisionSimulator");

            _RenderWindow.Closed += OnClosed;
            _RenderWindow.SetFramerateLimit(60);

            _Width = width;
            _Height = height;

            _scale = scale;
            MaxX = _scale * _Width;
            MaxY = _scale * _Height;

        }

        protected void DrawCircle(Vector2 pos, float radius)
        {
            float scaledRadius = radius / _scale;

            float scaledX = (pos.X / _scale) - scaledRadius;
            float scaledY = (float)_Height - (pos.Y / _scale) - scaledRadius;

            var circle = new SFML.Graphics.CircleShape(scaledRadius)
            {
                FillColor = SFML.Graphics.Color.White,
                Position = new SFML.System.Vector2f(scaledX, scaledY),
            };
            _RenderWindow.Draw(circle);

        }

        protected abstract void Update(float dt);

        public void Run()
        {
            uint frames = 0;
            float accTime = 0;  // seconds

            Debug.Assert(_run == false);
            _run = true;

            SFML.System.Clock clock = new();

            // Start the game loop
            while (_RenderWindow.IsOpen)
            {
                // Process events
                _RenderWindow.DispatchEvents();

                _RenderWindow.Clear();

                float dt = clock.ElapsedTime.AsSeconds();
                clock.Restart();

                Update(dt);

                // Finally, display the rendered frame on screen
                _RenderWindow.Display();

                accTime += dt;
                frames++;

                if(accTime >= 1.0f)
                {
                    Console.WriteLine($"FPS: {frames}");
                    frames = 0;
                    accTime -= 1.0f;
                }
            }

        }

        private void OnClosed(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(sender != null);

            var Window = (SFML.Window.Window)sender;
            Window.Close();
        }
    }

    abstract class Force(Vector2 value)
    {
        protected readonly Vector2 _value = value;
        public Vector2 Value { get { return _value; } }
    }

    class Gravity : Force
    {
        private static readonly Vector2 _G = new(0, -9.8f);

        public Gravity(float m) : base(m * _G) 
        {
            Debug.Assert(m > 0);
        }
    }

    class AerodynamicDrag : Force
    {
        private static float DragForceConstant(Vector2 vel, float radius)
        {
            var squared = vel.LengthSquared();
            var p = 1.0f;  // fluid density, but it is not exactly.
            var a = (float)Math.PI * radius * radius;  // characteristic body area, pi * r^2, for sphere.
            var c = 1.2f;  // drag coefficient for Circle in Two-Dimentional.

            /*return (-0.5f * p * squared * a * c) / (float)Math.Sqrt(squared);*/
            return -0.5f * p * (float)Math.Sqrt(squared) * a * c;
        }

        public AerodynamicDrag(Vector2 vel, float radius) 
            : base(DragForceConstant(vel, radius) * vel)
        { }
    }

    class Ball
    {
        private Vector2 _p, _v;  // m
        public Vector2 Pos { get { return _p; } }
        public Vector2 Vel { set { _v = value; } get { return _v; } }

        private readonly float _m;  // kg
        public float Mass { get { return _m; } }

        private readonly float _r;  // m
        public float Radius { get { return _r; } }

        public Ball(Vector2 pos, Vector2 vel, float mass, float radius)
        {
            Debug.Assert(radius > 0);

            _p = pos;
            _v = vel;
            _m = mass;
            _r = radius;
        }

        public void Apply(float dt, params Force[] forces)
        {
            var totalForce = new Vector2(0, 0);

            foreach (Force f in forces)
                totalForce += f.Value;

            var acc = new Vector2((totalForce.X / Mass), (totalForce.Y / Mass));

            _v.X += acc.X * dt;
            _v.Y += acc.Y * dt;
            _p.X += _v.X * dt;
            _p.Y += _v.Y * dt;

            /*Console.WriteLine($"Position: {_p}");*/
        }

    }

    class Game : Window
    {
        private Ball _b1, _b2;

        public Game() : base(800, 600, 0.1f)
        {
            float a = 100.0f;
            _b1 = new(new(0, MaxY / 2), new(a, 0), 100.0f, 2.0f);
            _b2 = new(new(MaxX, MaxY / 2), new(-a, 0), 100.0f, 2.0f);
        }

        protected override void Update(float dt)
        {
            /*Console.WriteLine(dt);*/
            Debug.Assert(dt > 0);

            // check collision
            /*
             * 충돌 감지 > 두 오브젝트를 겹치기 전으로 조정 > 두 오브젝트의 충돌 후의 속도를 계산 (옵션)
             * 아래의 코드에는 겹치기 전으로 조정하는 기능은 포함되어 있지 않음.
             */
            float dx = (_b1.Pos.X - _b2.Pos.X);
            float dy = (_b1.Pos.Y - _b2.Pos.Y);
            float dSquared = (dx * dx) + (dy * dy);
            float r = _b1.Radius + _b2.Radius;
            float rSquared = r * r;
            if (dSquared <= rSquared)
            {
                /*Console.WriteLine("Collision!");*/

                float e = 0.9f;

                Vector2 v1 = _b1.Vel, v2 = _b2.Vel;
                float m1 = _b1.Mass, m2 = _b2.Mass;

                float angle;
                float p1, n1, p2, n2;
                Vector2 v1Prime, v2Prime;
                if (dx == 0)  // 90 degrees
                {
                    p1 = v1.Y;
                    n1 = v1.X;
                    p2 = v2.Y;
                    n2 = v2.X;

                    var p1Prime =
                        (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                        ((((1 + e) * m2) / (m1 + m2)) * p2);
                    var p2Prime =
                        ((((1 + e) * m1) / (m1 + m2)) * p1) +
                        (((m2 - (e * m1)) / (m1 + m2)) * p2);

                    v1Prime = new(n1, p1Prime);
                    v2Prime = new(n2, p2Prime);
                }
                else if (dy == 0)  // 0 degrees
                {
                    p1 = v1.X;
                    n1 = v1.Y;
                    p2 = v2.X;
                    n2 = v2.Y;

                    var p1Prime =
                        (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                        ((((1 + e) * m2) / (m1 + m2)) * p2);
                    var p2Prime =
                        ((((1 + e) * m1) / (m1 + m2)) * p1) +
                        (((m2 - (e * m1)) / (m1 + m2)) * p2);

                    v1Prime = new(p1Prime, n1);
                    v2Prime = new(p2Prime, n2);
                }
                else
                {
                    angle = (float)Math.Atan(dy / dx);
                    p1 =
                        (v1.X * (float)Math.Cos(angle)) +
                        (v1.Y * (float)Math.Sin(angle));
                    n1 =
                        (v1.X * -(float)Math.Sin(angle)) +
                        (v1.Y * (float)Math.Cos(angle));
                    p2 =
                        (v2.X * (float)Math.Cos(angle)) +
                        (v2.Y * (float)Math.Sin(angle));
                    n2 =
                        (v2.X * -(float)Math.Sin(angle)) +
                        (v2.Y * (float)Math.Cos(angle));

                    var p1Prime =
                        (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                        ((((1 + e) * m2) / (m1 + m2)) * p2);
                    var p2Prime =
                        ((((1 + e) * m1) / (m1 + m2)) * p1) +
                        (((m2 - (e * m1)) / (m1 + m2)) * p2);

                    v1Prime = new(
                        (p1Prime * (float)Math.Cos(angle)) +
                        (n1 * -(float)Math.Sin(angle)),
                        (p1Prime * (float)Math.Sin(angle)) +
                        (n1 * (float)Math.Cos(angle)));
                    v2Prime = new(
                        (p2Prime * (float)Math.Cos(angle)) +
                        (n2 * -(float)Math.Sin(angle)),
                        (p2Prime * (float)Math.Sin(angle)) +
                        (n2 * (float)Math.Cos(angle)));
                }

                _b1.Vel = v1Prime;
                _b2.Vel = v2Prime;
            }

            _b1.Apply(dt, 
                new Gravity(_b1.Mass), 
                new AerodynamicDrag(_b1.Vel, _b1.Radius));
            DrawCircle(_b1.Pos, _b1.Radius);

            _b2.Apply(dt, 
                new Gravity(_b2.Mass), 
                new AerodynamicDrag(_b2.Vel, _b2.Radius));
            DrawCircle(_b2.Pos, _b2.Radius);
        }
    }

    class SimpleTwoBallsPlainCollisionSimulator
    {
        /*
         * Get Post-collision velocities, v1' and v2', in Linear.
         * 
         * This calculation is for two objects 
         * colliding without overlapping. That is, 
         * the distance d between the two objects 
         * is equal to the sum of their radii, r1 + r2. 
         * 
         * The coefficient of restitution, e.
         * 
         * If e = 1 and mass is same, 
         * the pre- and post-collision 
         * relative velocities are equal, 
         * meaning that the collision is elastic.
         * If e = 0 and mass is same, 
         * the objects are stuck together 
         * and the collision is completely inelastic.
         */
        private static void F1(
            float e, 
            float v1, float m1, float v2, float m2)
        {
            Debug.Assert(0 <= e && e <= 1);
            Debug.Assert(m1 > 0);
            Debug.Assert(m2 > 0);

            Console.WriteLine(
                "Get Post-collision velocities, " +
                "v1' and v2', in Linear.");

            Console.WriteLine("Requires:");
            Console.WriteLine($"\te: {e}");
            Console.WriteLine($"\tv1: {v1}");
            Console.WriteLine($"\tm1: {m1}");
            Console.WriteLine($"\tv2: {v2}");
            Console.WriteLine($"\tm2: {m2}");

            var v1Prime = 
                (((m1 - (e * m2)) / (m1 + m2)) * v1) + 
                ((((1 + e) * m2) / (m1 + m2)) * v2);
            var v2Prime = 
                ((((1 + e) * m1) / (m1 + m2)) * v1) + 
                (((m2 - (e * m1)) / (m1 + m2)) * v2);
            Console.WriteLine("Results:");
            Console.WriteLine($"\tv1': {v1Prime}");
            Console.WriteLine($"\tv2': {v2Prime}");
        }

        /*
         * Get Post-collision velocity v1' 
         * in Linear with immovable object.
         * 
         * This calculation is for two objects 
         * colliding without overlapping. That is, 
         * the distance d between the two objects 
         * is equal to the sum of their radii, r1 + r2. 
         * 
         * The coefficient of restitution, e.
         * 
         * If e = 1, the pre- and post-collision velocities 
         * have the same magnitude but the opposite directions.
         * If e = 0, the post-collision velocity of the 
         * moving object is zero (the moving object would 
         * stick to the immovable object);
         * 
         * The immovable object's velocity is assumed to be 
         * zero and mass to be infinite.
         */
        private static void F2(
            float e,
            float v1, float m1)
        {
            Debug.Assert(0 <= e && e <= 1);
            Debug.Assert(m1 > 0);

            Console.WriteLine(
                "Get Post-collision velocity v1' " +
                "in Linear with immovable object.");

            Console.WriteLine("Requires:");
            Console.WriteLine($"\te: {e}");
            Console.WriteLine($"\tv1: {v1}");
            Console.WriteLine($"\tm1: {m1}");

            var v1Prime = -e * v1;
            Console.WriteLine("Results:");
            Console.WriteLine($"\tv1': {v1Prime}");
        }

        /*
         * Get post-collision velocities, v1' and v2', of 
         * two shpere objects in plain.
         * 
         * This calculation is for two objects 
         * colliding without overlapping. That is, 
         * the distance d between the two objects 
         * is equal to the sum of their radii, r1 + r2. 
         * 
         * The coefficient of restitution, e.
         * 
         * If e = 1 and mass is same, 
         * the pre- and post-collision 
         * relative velocities are equal, 
         * meaning that the collision is elastic.
         * If e = 0 and mass is same, 
         * the objects are stuck together 
         * and the collision is completely inelastic.
         */
        private static void G1(
            float e, 
            Vector2 pos1, Vector2 v1, float m1, float r1,
            Vector2 pos2, Vector2 v2, float m2, float r2)
        {
            Debug.Assert(0 <= e && e <= 1);
            Debug.Assert(m1 > 0);
            Debug.Assert(r1 > 0);
            Debug.Assert(m2 > 0);
            Debug.Assert(r2 > 0);

            Console.WriteLine(
                "Get post-collision velocities, v1' and v2', of " +
                "two shpere objects in plain.");

            Console.WriteLine("Requires:");
            Console.WriteLine($"\te: {e}");
            Console.WriteLine($"\tObject1:");
            Console.WriteLine($"\t\tposition: {pos1}");
            Console.WriteLine($"\t\tvelocity: {v1}");
            Console.WriteLine($"\t\tmass: {m1}");
            Console.WriteLine($"\t\tradius: {r1}");
            Console.WriteLine($"\tObject2:");
            Console.WriteLine($"\t\tposition: {pos2}");
            Console.WriteLine($"\t\tvelocity: {v2}");
            Console.WriteLine($"\t\tmass: {m2}");
            Console.WriteLine($"\t\tradius: {r2}");

            float dx = pos1.X - pos2.X;
            float dy = pos1.Y - pos2.Y;

            float dSquared = (dx * dx) + (dy * dy);
            Debug.Assert(dSquared == (r2 + r1) * (r2 + r1));

            float angle;
            float p1, n1, p2, n2;
            Vector2 v1Prime, v2Prime;
            if (dx == 0)  // 90 degrees
            {
                p1 = v1.Y;
                n1 = v1.X;
                p2 = v2.Y;
                n2 = v2.X;

                var p1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                    ((((1 + e) * m2) / (m1 + m2)) * p2);
                var p2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * p1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * p2);

                v1Prime = new(n1, p1Prime);
                v2Prime = new(n2, p2Prime);
            }
            else if (dy == 0)  // 0 degrees
            {
                p1 = v1.X;
                n1 = v1.Y;
                p2 = v2.X;
                n2 = v2.Y;

                var p1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                    ((((1 + e) * m2) / (m1 + m2)) * p2);
                var p2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * p1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * p2);

                v1Prime = new(p1Prime, n1);
                v2Prime = new(p2Prime, n2);
            }
            else
            {
                angle = (float)Math.Atan(dy / dx);
                p1 =
                    (v1.X * (float)Math.Cos(angle)) +
                    (v1.Y * (float)Math.Sin(angle));
                n1 =
                    (v1.X * -(float)Math.Sin(angle)) +
                    (v1.Y * (float)Math.Cos(angle));
                p2 =
                    (v2.X * (float)Math.Cos(angle)) +
                    (v2.Y * (float)Math.Sin(angle));
                n2 =
                    (v2.X * -(float)Math.Sin(angle)) +
                    (v2.Y * (float)Math.Cos(angle));

                var p1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * p1) +
                    ((((1 + e) * m2) / (m1 + m2)) * p2);
                var p2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * p1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * p2);

                v1Prime = new(
                    (p1Prime * (float)Math.Cos(angle)) +
                    (n1 * -(float)Math.Sin(angle)),
                    (p1Prime * (float)Math.Sin(angle)) +
                    (n1 * (float)Math.Cos(angle)));
                v2Prime = new(
                    (p2Prime * (float)Math.Cos(angle)) +
                    (n2 * -(float)Math.Sin(angle)),
                    (p2Prime * (float)Math.Sin(angle)) +
                    (n2 * (float)Math.Cos(angle)));
            }

            Console.WriteLine("Results:");
            Console.WriteLine($"\tv1': {v1Prime}");
            Console.WriteLine($"\tv2': {v2Prime}");
        }

        static void Main()
        {
            Console.WriteLine("Hello, World!");

            /*F1(0.0f, 3.0f, 10.0f, -3.0f, 10.0f);
            F1(0.9f, 3.0f, 10.0f, -3.0f, 20.0f);
            F2(0.0f, 3.0f, 10.0f);

            G1(
                1.0f,
                new(0.0f, 0.0f), new(3.0f, 4.0f), 10.0f, 2,
                new(3.0f, 4.0f), new(-3.0f, -4.0f), 10.0f, 3);
            G1(
                0.0f,
                new(0.0f, 0.0f), new(3.0f, 4.0f), 10.0f, 2,
                new(3.0f, 4.0f), new(-3.0f, -4.0f), 10.0f, 3);
            G1(
                0.9f,
                new(0.0f, 0.0f), new(3.0f, 1.0f), 20.0f, 2,
                new(3.0f, 4.0f), new(-3.0f, -4.0f), 10.0f, 3);
            G1(
                0.0f,
                new(0.0f, 0.0f), new(3.0f, 1.0f), 20.0f, 2,
                new(3.0f, 4.0f), new(-3.0f, -4.0f), 10.0f, 3);
            G1(
                1.0f,
                new(1.0f, -1.0f), new(0.0f, 3.0f), 10.0f, 3,
                new(1.0f, 4.0f), new(0.0f, -3.0f), 10.0f, 2);
            G1(
                0.9f,
                new(1.0f, -1.0f), new(1.0f, 2.0f), 10.0f, 3,
                new(1.0f, 4.0f), new(-1.0f, -3.0f), 10.0f, 2);
            G1(
                0.9f,
                new(-3.0f, -1.0f), new(1.0f, 2.0f), 15.0f, 4,
                new(4.0f, -1.0f), new(-1.0f, -3.0f), 10.0f, 3);*/

            var game = new Game();
            game.Run();
        }
    }
}