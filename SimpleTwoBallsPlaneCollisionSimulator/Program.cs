
using SFML.System;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Collections.Generic;
using SFML.Graphics;
using System.Runtime.Intrinsics.X86;

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
            /*_RenderWindow.SetFramerateLimit(60);*/

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

        protected void DrawLineSegment(Vector2 p, Vector2 n, float h)
        {
            Debug.Assert(h > 0);

            float scaledX = (p.X / _scale);
            float scaledY = (float)_Height - (p.Y / _scale);
            float scaledH = h / _scale;

            SFML.Graphics.Vertex[] line = new SFML.Graphics.Vertex[2];

            float dx = n.X; float dy = n.Y;
            if (dx == 0)  // vertical
            {
                line[0] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX - scaledH, scaledY));
                line[1] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX + scaledH, scaledY));
            }
            else if (dy == 0)  // horizontal
            {
                line[0] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX, scaledY - scaledH));
                line[1] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX, scaledY + scaledH));
            }
            else
            {
                float tangent = -dy / dx;
                float a1 = (float)Math.Atan(tangent);
                float a2 = ((float)Math.PI / 2.0f) - a1;
                float dxPrime = scaledH * (float)Math.Cos(a1);
                float dyPrime = scaledH * (float)Math.Cos(a2);

                if (tangent > 0)
                {
                    line[0] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX - dxPrime, scaledY - dyPrime));
                    line[1] = new SFML.Graphics.Vertex(
                        new SFML.System.Vector2f(scaledX + dxPrime, scaledY + dyPrime));
                }
                else
                {
                    Debug.Assert(tangent < 0);
                    line[0] = new SFML.Graphics.Vertex(
                    new SFML.System.Vector2f(scaledX + dxPrime, scaledY - dyPrime));
                    line[1] = new SFML.Graphics.Vertex(
                        new SFML.System.Vector2f(scaledX - dxPrime, scaledY + dyPrime));
                }
            }

            _RenderWindow.Draw(line, SFML.Graphics.PrimitiveType.Lines);
        }

        protected abstract void Init();

        protected abstract void Update(float dt);

        public void Run()
        {
            uint frames = 0;
            float accTime = 0;  // seconds

            Debug.Assert(_run == false);
            _run = true;

            // TODO: 초반에 핑이 튀는 구간이 있음.
            SFML.System.Clock clock = new();

            Init();
            _RenderWindow.Display();

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
        private static readonly Vector2 _G = new(0, -1.8f);

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

            // characteristic body area, pi * r^2, for sphere.
            var a = (float)Math.PI * radius * radius;  
            var c = 1.2f;  // drag coefficient for Circle in Two-Dimentional.

            return (-0.5f * p * squared * a * c) / (float)Math.Sqrt(squared);
        }

        public AerodynamicDrag(Vector2 vel, float radius) 
            : base(DragForceConstant(vel, radius) * vel)
        { }
    }

    static class CollisionResolution
    {
        // the coefficient of restitution
        private static readonly float E = 0.6f;

        private static void PostCollisionVelocities1(
                MovableObject obj1, MovableObject obj2)
        {
            Debug.Assert(0 <= E && E <= 1);

            float dx = obj2.Position.X - obj1.Position.X;
            float dy = obj2.Position.Y - obj1.Position.Y;

            float c1, n1, c2, n2;
            float c1Prime, c2Prime;
            if (dx == 0)  // 90 degrees
            {
                c1 = obj1.Velocity.Y;
                n1 = obj1.Velocity.X;
                c2 = obj2.Velocity.Y;
                n2 = obj2.Velocity.X;

                c1Prime =
                    (((obj1.Mass - (E * obj2.Mass)) / (obj1.Mass + obj2.Mass)) * c1) +
                    ((((1 + E) * obj2.Mass) / (obj1.Mass + obj2.Mass)) * c2);
                c2Prime =
                    ((((1 + E) * obj1.Mass) / (obj1.Mass + obj2.Mass)) * c1) +
                    (((obj2.Mass - (E * obj1.Mass)) / (obj1.Mass + obj2.Mass)) * c2);

                obj1.Velocity = new(n1, c1Prime);
                obj2.Velocity = new(n2, c2Prime);
            }
            else if (dy == 0)  // 0 degrees
            {
                c1 = obj1.Velocity.X;
                n1 = obj1.Velocity.Y;
                c2 = obj2.Velocity.X;
                n2 = obj2.Velocity.Y;

                c1Prime =
                    (((obj1.Mass - (E * obj2.Mass)) / (obj1.Mass + obj2.Mass)) * c1) +
                    ((((1 + E) * obj2.Mass) / (obj1.Mass + obj2.Mass)) * c2);
                c2Prime =
                    ((((1 + E) * obj1.Mass) / (obj1.Mass + obj2.Mass)) * c1) +
                    (((obj2.Mass - (E * obj1.Mass)) / (obj1.Mass + obj2.Mass)) * c2);

                obj1.Velocity = new(c1Prime, n1);
                obj2.Velocity = new(c2Prime, n2);
            }
            else
            {
                float angle = (float)Math.Atan(dy / dx);

                c1 =
                    (obj1.Velocity.X * (float)Math.Cos(angle)) +
                    (obj1.Velocity.Y * (float)Math.Sin(angle));
                n1 =
                    (obj1.Velocity.X * -(float)Math.Sin(angle)) +
                    (obj1.Velocity.Y * (float)Math.Cos(angle));
                c2 =
                    (obj2.Velocity.X * (float)Math.Cos(angle)) +
                    (obj2.Velocity.Y * (float)Math.Sin(angle));
                n2 =
                    (obj2.Velocity.X * -(float)Math.Sin(angle)) +
                    (obj2.Velocity.Y * (float)Math.Cos(angle));

                c1Prime =
                    (((obj1.Mass - (E * obj2.Mass)) / (obj1.Mass + obj2.Mass)) * c1) +
                    ((((1 + E) * obj2.Mass) / (obj1.Mass + obj2.Mass)) * c2);
                c2Prime =
                    ((((1 + E) * obj1.Mass) / (obj1.Mass + obj2.Mass)) * c1) +
                    (((obj2.Mass - (E * obj1.Mass)) / (obj1.Mass + obj2.Mass)) * c2);

                obj1.Velocity = new(
                    (c1Prime * (float)Math.Cos(angle)) +
                    (n1 * -(float)Math.Sin(angle)),
                    (c1Prime * (float)Math.Sin(angle)) +
                    (n1 * (float)Math.Cos(angle)));
                obj2.Velocity = new(
                    (c2Prime * (float)Math.Cos(angle)) +
                    (n2 * -(float)Math.Sin(angle)),
                    (c2Prime * (float)Math.Sin(angle)) +
                    (n2 * (float)Math.Cos(angle)));
            }

        }

        private static void PostCollisionVelocities2(
                MovableObject obj, Vector2 nPlain)
        {
            Debug.Assert(0 <= E && E <= 1);

            float dx = nPlain.X; float dy = nPlain.Y;

            float c, n, cPrime;
            if (dx == 0)  // vertical
            {
                c = obj.Velocity.Y;
                n = obj.Velocity.X;

                cPrime = -E * c;
                obj.Velocity = new(n, cPrime);
            }
            else if (dy == 0)  // horizontal
            {
                c = obj.Velocity.X;
                n = obj.Velocity.Y;

                cPrime = -E * c;
                obj.Velocity = new(cPrime, n);
            }
            else
            {
                float angle = (float)Math.Atan(dy / dx);

                c =
                    (obj.Velocity.X * (float)Math.Cos(angle)) +
                    (obj.Velocity.Y * (float)Math.Sin(angle));
                n =
                    (obj.Velocity.X * -(float)Math.Sin(angle)) +
                    (obj.Velocity.Y * (float)Math.Cos(angle));

                cPrime = -E * c;
                obj.Velocity = new(
                    (cPrime * (float)Math.Cos(angle)) +
                    (n * -(float)Math.Sin(angle)),
                    (cPrime * (float)Math.Sin(angle)) +
                    (n * (float)Math.Cos(angle)));
            }

        }

        private static void BallToBall(Ball b1, Ball b2)
        {
            Vector2 a = b1.Position - b2.Position;
            float s1 = Vector2.Dot(a, a);
            float d2 = b1.Radius + b2.Radius;
            float s2 = (float)Math.Pow(d2, 2);
            Debug.Assert(s1 > 0);
            Debug.Assert(d2 > 0);
            Debug.Assert(s2 > 0);
            if (s1 > s2)
                return;

            float d1 = (float)Math.Sqrt(s1);
            Debug.Assert(d1 > 0);

            float g1 = d2 - d1;
            Debug.Assert(g1 >= 0);

            if (g1 > 0)
            {
                var g2 = g1 / 2;
                Debug.Assert(g2 > 0);

                Vector2 u = a / (float)Math.Sqrt(s1);
                Vector2 e1 = g2 * u;
                Vector2 e2 = -g2 * u;

                b1.Position += e1;
                b2.Position += e2;
            }

            PostCollisionVelocities1(b1, b2);

        }

        private static void BallToWall(Ball b1, Wall w2)
        {

            float d = Vector2.Dot(w2.Normal, b1.Position) - w2.Distance;
            if (d <= 0 || d > b1.Radius) 
                return;

            Debug.Assert(b1.Radius > 0);
            float c;
            {
                var x = (b1.Radius * b1.Radius) - (d * d);
                c = (float)Math.Sqrt(x);
            }
            Debug.Assert(c >= 0);

            var a = b1.Position - (d * w2.Normal);
            if (Vector2.Distance(a, w2.Position) > w2.HalfLength + c)
                return;

            float g = b1.Radius - d;
            if (g > 0)
            {
                Vector2 e = w2.Normal * g;
                b1.Position += e;
            }

            PostCollisionVelocities2(b1, w2.Normal);

            return;
        }

        public static void ObjectToObject(Object o1, Object o2)
        {
            if (o1 is Ball b1)
                if (o2 is Ball b2)
                {
                    BallToBall(b1, b2);
                    return;
                }
                else if (o2 is Wall w2)
                {
                    BallToWall(b1, w2);
                    return;
                }
                else
                    throw new NotImplementedException();
            else if (o1 is Wall w1)
                if (o2 is Ball b2)
                {
                    BallToWall(b2, w1);
                    return;
                }
                else if (o2 is Wall)
                {
                    return;
                }
                else
                    throw new NotImplementedException();
            else
                throw new NotImplementedException();
        }
    }

    abstract class Object
    {
        protected Vector2 _p;  // m
        public Vector2 Position { set { _p = value; } get { return _p; } }

        public Object(Vector2 p)
        {
            _p = p;
        }

        public abstract void F1(float dt);

        public abstract bool F2(
            float minX, float minY, float maxX, float maxY);

        public void F3(Queue<Object> objs)
        {
            foreach (Object obj in objs)
                CollisionResolution.ObjectToObject(this, obj);
        }
    }

    abstract class MovableObject : Object
    {
        protected Vector2 _v;  // m
        public Vector2 Velocity { set { _v = value; } get { return _v;} }

        protected readonly float _m;  // kg
        public float Mass { get { return _m; } }

        public MovableObject(Vector2 p, Vector2 v, float m) : base(p)
        {
            Debug.Assert(m > 0);

            _v = v; 
            _m = m;

        }

        public override void F1(float dt) 
        {
            Debug.Assert(dt > 0);

            var forceAcc = new Vector2(0, 0);
            forceAcc += new Gravity(_m).Value;

            _v.X += (forceAcc.X / _m) * dt;
            _v.Y += (forceAcc.Y / _m) * dt;

            _p.X += _v.X * dt;
            _p.Y += _v.Y * dt;
        }

    }

    abstract class ImmovableObject : Object
    {
        public ImmovableObject(Vector2 p) : base(p) { }
        
        public override void F1(float dt)
        {
            Debug.Assert(dt > 0);
        }

        public override bool F2(
            float minX, float minY, float maxX, float maxY)
        {
            Debug.Assert(minX < maxX);
            Debug.Assert(minY < maxY);
            return false;
        }

    }

    class Ball : MovableObject
    {
        private readonly float _r;  // m
        public float Radius { get { return _r; } }

        public Ball(
            Vector2 position, Vector2 velocity, float mass, 
            float radius) 
            : base(position, velocity, mass)
        {
            Debug.Assert(radius > 0);

            _r = radius;
        }

        public override bool F2(
            float minX, float minY, float maxX, float maxY)
        {
            return (_p.X + _r < minX ||
                    _p.Y + _r < minY ||
                    _p.X - _r > maxX ||
                    _p.Y - _r > maxY);
        }
    }

    class Wall : ImmovableObject
    {
        private readonly float _h;  // m
        public float Length {  get { return _h * 2.0f; } }
        public float HalfLength { get { return _h; } }

        private readonly Vector2 _n;
        public Vector2 Normal { get { return _n; } }

        private readonly float _d;  // m
        public float Distance { get { return _d; } }

        public Wall(
            Vector2 position, 
            float length, Vector2 n) 
            : base(position) 
        {
            Debug.Assert(length > 0);

            _h = length / 2.0f;
            _n = n;
            _d = Vector2.Dot(_p, _n);
            
        }

    }

    class Game : Window
    {
        private Queue<Object> _objs = new();

        public Game(params Object[] objs) : base(800, 600, 0.01f)
        {
            foreach (Object o in objs)
                _objs.Enqueue(o);
        }
        
        protected override void Init()
        {

            // TODO: Refactoring
            /*foreach (Object _obj in _objs)
            {
                // TODO
                if (_obj is Ball b)
                    DrawCircle(b.Position, b.Radius);
                else if (_obj is Wall w)
                    DrawLineSegment(w.Position, w.Normal, w.HalfLength);
                else
                    throw new NotImplementedException();

            }*/
        }

        protected override void Update(float dt)
        {
            /*Console.WriteLine(dt);*/
            Debug.Assert(dt > 0);

            int length = _objs.Count();
            for (int i = 0; i < length; ++i)
            {
                Object obj = _objs.Dequeue();

                // TODO
                if (obj is Ball b)
                    DrawCircle(obj.Position, b.Radius);
                else if (obj is Wall w)
                    DrawLineSegment(w.Position, w.Normal, w.HalfLength);
                else
                    throw new NotImplementedException();

                obj.F1(dt);

                if (obj.F2(MinX, MinY, MaxX, MaxY))
                    continue;

                _objs.Enqueue(obj);
            }

            Queue<Object> objs = new();

            length = _objs.Count();
            for (int i = 0; i < length; ++i)
            {
                Object obj = _objs.Dequeue();

                obj.F3(_objs);

                objs.Enqueue(obj);
            }

            Debug.Assert(_objs.Count() == 0);
            _objs = objs;
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
                new Ball(new(3.5f, 4.0f), new(1.0f, 0.0f), 1.0f, 0.2f),
                new Ball(new(6.5f, 4.0f), new(-1.0f, 0.0f), 1.5f, 0.3f),
                new Ball(new(5.0f, 6.0f), new(0.0f, -2.0f), 1.5f, 0.5f),
                new Ball(new(5.0f, 2.0f), new(0.0f, -2.0f), 1.5f, 0.4f),
                new Wall(new(4.0f, 0.0f), 8.0f, Vector2.Normalize(new(0.0f, 1.0f))),
                new Wall(new(0.0f, 3.0f), 6.0f, Vector2.Normalize(new(1.0f, 0.0f))),
                new Wall(new(8.0f, 3.0f), 6.0f, Vector2.Normalize(new(-1.0f, 0.0f))),
                new Wall(new(1.0f, 1.0f), 3.0f, Vector2.Normalize(new(1.0f, 1.0f))));
            game.Run();
        }
    }
}