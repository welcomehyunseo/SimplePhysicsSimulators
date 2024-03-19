
using SFML.System;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Collections.Generic;
using SFML.Graphics;

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

        public readonly float MinX, MinY;
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
            MinX = 0; MinY = 0;
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

        protected abstract void Init();

        protected abstract void Update(float dt);

        public void Run()
        {
            uint frames = 0;
            float accTime = 0;  // seconds

            Debug.Assert(_run == false);
            _run = true;

            SFML.System.Clock clock = new();

            Init();

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
            var p = 0.05f;  // fluid density, but it is not exactly.
            var a = (float)Math.PI * radius * radius;  // characteristic body area, pi * r^2, for sphere.
            var c = 1.2f;  // drag coefficient for Circle in Two-Dimentional.

            return (-0.5f * p * squared * a * c) / (float)Math.Sqrt(squared);
        }

        public AerodynamicDrag(Vector2 vel, float radius) 
            : base(DragForceConstant(vel, radius) * vel)
        { }
    }

    abstract class Object
    {
        protected Vector2 _p;  // m
        public Vector2 Position { get { return _p; } }

        public Object(Vector2 p)
        {
            _p = p;
        }

        public abstract void F1(float dt, Queue<Object> objs);

        public abstract void F2(MovableObject obj);

        public abstract bool F3(
            float minX, float minY, float maxX, float maxY);

    }

    abstract class MovableObject : Object
    {
        protected Vector2 _v;  // m
        public Vector2 Velocity { get { return _v;} }

        protected readonly float _m;  // kg
        public float Mass { get { return _m; } }

        public MovableObject(Vector2 p, Vector2 v, float m) : base(p)
        {
            Debug.Assert(m > 0);

            _v = v; 
            _m = m;
        }

        public override void F1(float dt, Queue<Object> objs) 
        {
            var forceAcc = new Vector2(0, 0);
            forceAcc += new Gravity(Mass).Value;

            _v.X += (forceAcc.X / _m) * dt;
            _v.Y += (forceAcc.Y / _m) * dt;

            _p.X += _v.X * dt; 
            _p.Y += _v.Y * dt;

            foreach (Object obj in objs)
                obj.F2(this);

        }
    }

    abstract class ImmovableObject : Object
    {
        public ImmovableObject(Vector2 p) : base(p) { }

        public override void F1(float dt, Queue<Object> otherObjs) { }
    }

    class Ball : MovableObject
    {
        private readonly float _r;  // m
        public float Radius { get { return _r; } }

        public Ball(Vector2 p, Vector2 v, float m, float r) : base(p, v, m)
        {
            Debug.Assert(r > 0);

            _r = r;
        }

        private static (Vector2, Vector2) ToPostCollisionVelocities(
            Vector2 p1, Vector2 v1, float m1,
            Vector2 p2, Vector2 v2, float m2)
        {
            float e = 0.9f;

            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;

            float c1, n1, c2, n2;
            Vector2 v1Prime, v2Prime;
            if (dx == 0)  // 90 degrees
            {
                c1 = v1.Y;
                n1 = v1.X;
                c2 = v2.Y;
                n2 = v2.X;

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(n1, c1Prime);
                v2Prime = new(n2, c2Prime);
            }
            else if (dy == 0)  // 0 degrees
            {
                c1 = v1.X;
                n1 = v1.Y;
                c2 = v2.X;
                n2 = v2.Y;

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(c1Prime, n1);
                v2Prime = new(c2Prime, n2);
            }
            else
            {
                float angle = (float)Math.Atan(dy / dx);

                c1 =
                    (v1.X * (float)Math.Cos(angle)) +
                    (v1.Y * (float)Math.Sin(angle));
                n1 =
                    (v1.X * -(float)Math.Sin(angle)) +
                    (v1.Y * (float)Math.Cos(angle));
                c2 =
                    (v2.X * (float)Math.Cos(angle)) +
                    (v2.Y * (float)Math.Sin(angle));
                n2 =
                    (v2.X * -(float)Math.Sin(angle)) +
                    (v2.Y * (float)Math.Cos(angle));

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(
                    (c1Prime * (float)Math.Cos(angle)) +
                    (n1 * -(float)Math.Sin(angle)),
                    (c1Prime * (float)Math.Sin(angle)) +
                    (n1 * (float)Math.Cos(angle)));
                v2Prime = new(
                    (c2Prime * (float)Math.Cos(angle)) +
                    (n2 * -(float)Math.Sin(angle)),
                    (c2Prime * (float)Math.Sin(angle)) +
                    (n2 * (float)Math.Cos(angle)));
            }

            return (v1Prime, v2Prime);
        }

        public override void F2(MovableObject _obj)
        {
            if (_obj is not Ball)
                throw new NotImplementedException();

            Ball b = (Ball)_obj;

            Vector2 d = _p - b._p;
            float distanceSquared = Vector2.Dot(d, d);
            if (distanceSquared <= (float)Math.Pow(_r + b._r, 2))
            {
                var distance = (float)Math.Sqrt(distanceSquared);
                var halfDistance = distance / 2;
                Vector2 u = d / distance;
                Vector2 e1 = halfDistance * u;
                Vector2 e2 = -halfDistance * u;

                _p += e1;
                b._p += e2;

                (_v, b._v) = ToPostCollisionVelocities(
                    _p, _v, _m, b._p, b._v, b._m);
            }

        }

        public override bool F3(
            float minX, float minY, float maxX, float maxY)
        {
            return (_p.X + _r < minX ||
                    _p.Y + _r < minY ||
                    _p.X - _r > maxX ||
                    _p.Y - _r > maxY);
        }
    }

    //class Wall : ImmovableObject
    //{
    //    public Wall(Vector2 p) : base(p) { }
    //}

    class Game : Window
    {
        private Queue<Object> _objs = new();

        public Game(params Ball[] balls) : base(800, 600, 0.01f)
        {
            foreach (Ball b in balls)
                _objs.Enqueue(b);
        }

        private static (Vector2, Vector2) ToPostCollisionVelocities(
            Vector2 p1, Vector2 v1, float m1, 
            Vector2 p2, Vector2 v2, float m2)
        {
            float e = 0.9f;

            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;

            float c1, n1, c2, n2;
            Vector2 v1Prime, v2Prime;
            if (dx == 0)  // 90 degrees
            {
                c1 = v1.Y;
                n1 = v1.X;
                c2 = v2.Y;
                n2 = v2.X;

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(n1, c1Prime);
                v2Prime = new(n2, c2Prime);
            }
            else if (dy == 0)  // 0 degrees
            {
                c1 = v1.X;
                n1 = v1.Y;
                c2 = v2.X;
                n2 = v2.Y;

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(c1Prime, n1);
                v2Prime = new(c2Prime, n2);
            }
            else
            {
                float angle = (float)Math.Atan(dy / dx);

                c1 =
                    (v1.X * (float)Math.Cos(angle)) +
                    (v1.Y * (float)Math.Sin(angle));
                n1 =
                    (v1.X * -(float)Math.Sin(angle)) +
                    (v1.Y * (float)Math.Cos(angle));
                c2 =
                    (v2.X * (float)Math.Cos(angle)) +
                    (v2.Y * (float)Math.Sin(angle));
                n2 =
                    (v2.X * -(float)Math.Sin(angle)) +
                    (v2.Y * (float)Math.Cos(angle));

                var c1Prime =
                    (((m1 - (e * m2)) / (m1 + m2)) * c1) +
                    ((((1 + e) * m2) / (m1 + m2)) * c2);
                var c2Prime =
                    ((((1 + e) * m1) / (m1 + m2)) * c1) +
                    (((m2 - (e * m1)) / (m1 + m2)) * c2);

                v1Prime = new(
                    (c1Prime * (float)Math.Cos(angle)) +
                    (n1 * -(float)Math.Sin(angle)),
                    (c1Prime * (float)Math.Sin(angle)) +
                    (n1 * (float)Math.Cos(angle)));
                v2Prime = new(
                    (c2Prime * (float)Math.Cos(angle)) +
                    (n2 * -(float)Math.Sin(angle)),
                    (c2Prime * (float)Math.Sin(angle)) +
                    (n2 * (float)Math.Cos(angle)));
            }

            return (v1Prime, v2Prime);
        }

        protected override void Init()
        {

            // TODO: Refactoring
            foreach (Object _obj in _objs)
            {
                // TODO
                if (_obj is not Ball) 
                    throw new NotImplementedException();

                Ball b = (Ball)_obj;
                DrawCircle(b.Position, b.Radius);
            }
        }

        protected override void Update(float dt)
        {
            /*Console.WriteLine(dt);*/
            Debug.Assert(dt > 0);

            int length = _objs.Count();
            for (int i = 0; i < length; ++i)
            {
                Object obj = _objs.Dequeue();
                obj.F1(dt, _objs);

                if (obj.F3(MinX, MinY, MaxX, MaxY))
                    continue;

                // TODO
                if (obj is Ball)
                    DrawCircle(obj.Position, ((Ball)obj).Radius);
                else
                    throw new NotImplementedException();

                _objs.Enqueue(obj);
            }
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

            var game = new Game(
                new Ball(new(0.0f, 5.0f), new(7.0f, 0.0f), 1.0f, 0.2f),
                new Ball(new(8.0f, 5.0f), new(-7.0f, 0.0f), 1.5f, 0.3f));
            game.Run();
        }
    }
}