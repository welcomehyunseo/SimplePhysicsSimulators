
using SFML.System;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata;
using System.Collections.Generic;
using SFML.Graphics;
using System.Runtime.Intrinsics.X86;
using static SimpleTwoBallsPlainCollisionSimulator.Block;
using System.Collections;

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

        public void DrawCircle(Vector2 p, float r)
        {
            Debug.Assert(_run == true);
            Debug.Assert(r > 0);

            float rScaled = r / _scale;

            float xScaled = (p.X / _scale) - rScaled;
            float yScaled = (float)_Height - (p.Y / _scale) - rScaled;

            var shape = new SFML.Graphics.CircleShape(rScaled)
            {
                // TODO: use static variable BackgroundColor of this class.
                FillColor = SFML.Graphics.Color.Black,
                OutlineColor = SFML.Graphics.Color.White,
                OutlineThickness = 1,
                Position = new SFML.System.Vector2f(xScaled, yScaled),
            };
            _RenderWindow.Draw(shape);
        }

        public void DrawLineSegment(Vector2 p, Vector2 n, float h)
        {
            Debug.Assert(_run == true);
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

        public void DrawSquare(Vector2 p, float s)
        {
            Debug.Assert(_run == true);
            Debug.Assert(s > 0);

            float sScaled = s / _scale;

            float xScaled = (p.X / _scale);
            float yScaled = (float)_Height - (p.Y / _scale) - sScaled;

            var shape = new SFML.Graphics.RectangleShape(new SFML.System.Vector2f(sScaled, sScaled))
            {
                // TODO: use static variable BackgroundColor of this class.
                FillColor = SFML.Graphics.Color.Black,
                OutlineColor = SFML.Graphics.Color.White,
                OutlineThickness = 1,
                Position = new SFML.System.Vector2f(xScaled, yScaled),
            };
            _RenderWindow.Draw(shape);
        }

        protected abstract void Update(float dt);

        public void Run()
        {
            uint frames = 0;
            float accTime = 0;  // seconds

            Debug.Assert(_run == false);
            _run = true;

            // TODO: 초반에 핑이 튀는 구간이 있음.
            /*
             * dt: 0.341817
             * dt: 0.027247
             * dt: 0.001187
             * ...
             * dt: 0.001234
             * dt: 0.292558
             */
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

                if (accTime >= 1.0f)
                {
                    Console.WriteLine($"FPS: {frames}");
                    frames = 0;
                    accTime -= 1.0f;
                }
            }

            Debug.Assert(_run == true);
            _run = false;

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

    static class CollisionDetector
    {
        private static (bool, float, Vector2) BallToBall(Ball b1, Ball b2)
        {
            Debug.Assert(b1 != b2);

            Vector2 a = b1.Position - b2.Position;
            
            float s1 = a.LengthSquared();
            Debug.Assert(s1 > 0);

            float d1 = (float)Math.Sqrt(s1);
            Debug.Assert(d1 > 0);

            Debug.Assert(b1.Radius > 0);
            Debug.Assert(b2.Radius > 0);
            float d2 = b1.Radius + b2.Radius;
            Debug.Assert(d2 > 0);

            // No collide.
            if (d1 > d2)
                return (false, 0, Vector2.Zero);

            float dPrime = d2 - d1;
            Debug.Assert(dPrime >= 0);

            // Not overlapped, just touches at one point.
            if (dPrime == 0)
                return (true, 0, Vector2.Zero);

            // Overlapped.
            Vector2 u = a / d1;
            return (true, dPrime, u);
        }

        private static (bool, float, Vector2) BallToBlock(Ball b1, Block b2)
        {
            if (b2.IsAllClosed())
                return (false, 0, Vector2.Zero);

            float d;
            Block.Face face;

            float y1, y2;

            y1 = (b1.Position.X - b2.Position.X) + b2.Position.Y;
            y2 = -(b1.Position.X - b2.Position.X) + (b2.Position.Y + Block.SideLength);
            if (b1.Position.Y >= y1)
            {
                if (b1.Position.Y >= y2)
                {
                    y1 = b1.Position.Y;
                    y2 = b2.Position.Y + Block.SideLength;

                    face = Face.Top;
                }
                else
                {
                    y1 = b2.Position.X;
                    y2 = b1.Position.X;

                    face = Face.Left;
                }
            }
            else
            {
                if (b1.Position.Y >= y2)
                {
                    y1 = b1.Position.X;
                    y2 = b2.Position.X + Block.SideLength;

                    face = Face.Right;
                }
                else
                {
                    y1 = b2.Position.Y;
                    y2 = b1.Position.Y;
                    
                    face = Face.Bottom;
                }
            }
            /*
             * If assertion failed,
             * the velocity or size of the objects, or the value dt was wrong.
             */
            Debug.Assert(y1 > y2);
            d = y1 - y2;

            float dPrime = b1.Radius - d;

            if (dPrime < 0)
                return (false, 0, Vector2.Zero);

            if (dPrime == 0)
                return (true, 0, Vector2.Zero);

            /*Console.WriteLine($"b2.Position: {b2.Position}");
            Console.WriteLine($"i: {i}");*/

            return (true, dPrime, GetNormal(face));
        }

        /*
         * bool f, float d, Vector2 u
         * f: isCollide
         * d: overlapDistance
         * u: unitVector, must be directed from o2 to o1, a = o1 - o2, u = a / |a|.
         */
        public static (bool, float, Vector2) IsCollided(Object o1, Object o2)
        {
            if (o1 is Ball ball1)
                if (o2 is Ball ball2)
                    return BallToBall(ball1, ball2);
                else if (o2 is Block block2)
                    return BallToBlock(ball1, block2);
                else
                    throw new NotImplementedException();
            else if (o1 is Block block1)
                if (o2 is Ball ball2)
                    return BallToBlock(ball2, block1);
                else if (o2 is Block)
                    return (false, 0, Vector2.Zero);
                else
                    throw new NotImplementedException();
            else
                throw new NotImplementedException();
        }
    }

    static class CollisionResolution
    {
        /* the coefficient of restitution, E.
         * 
         * If E = 1 and mass is same, 
         * the pre- and post-collision
         * relative velocities are equal, 
         * meaning that the collision is elastic.
         * If E = 0 and mass is same, 
         * the objects are stuck together
         * and the collision is completely inelastic.
         */
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

        private static void BallToBall(Ball b1, Ball b2, float d, Vector2 u)
        {
            Debug.Assert(b1 != b2);

            if (d > 0)
            {
                var h = d / 2;
                Debug.Assert(h > 0);

                Vector2 e1 = h * u;
                Vector2 e2 = -h * u;

                b1.Position += e1;
                b2.Position += e2;
            }

            PostCollisionVelocities1(b1, b2);

        }

        // TODO: Remove a unused variable, b2.
        private static void BallToBlock(Ball b1, Block b2, float d, Vector2 u)
        {
            if (d > 0)
            {
                Vector2 e = u * d;
                b1.Position += e;
            }

            PostCollisionVelocities2(b1, u);

            return;
        }

        public static void Handle(
            Object o1, Object o2, float d, Vector2 u)
        {
            // TODO: Check the vector u is an unit vector.

            if (o1 is Ball ball1)
                if (o2 is Ball ball2)
                    BallToBall(ball1, ball2, d, u);
                else if (o2 is Block block2)
                    BallToBlock(ball1, block2, d, u);
                else
                    throw new NotImplementedException();
            else if (o1 is Block block1)
                if (o2 is Ball ball2)
                    BallToBlock(ball2, block1, d, u);
                else if (o2 is Block)
                    Debug.Assert(true);
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
        
        public abstract void G1(Window window);
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

        public void F1(float dt) 
        {
            Debug.Assert(dt > 0);

            var forceAcc = new Vector2(0, 0);
            forceAcc += new Gravity(_m).Value;

            _v.X += (forceAcc.X / _m) * dt;
            _v.Y += (forceAcc.Y / _m) * dt;

            _p.X += _v.X * dt;
            _p.Y += _v.Y * dt;
        }

        public abstract bool F2(float minX, float minY, float maxX, float maxY);

    }

    abstract class ImmovableObject : Object
    {
        public ImmovableObject(Vector2 p) : base(p) { }
        
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

        public override void G1(Window window)
        {
            window.DrawCircle(_p, _r);
        }
    }

    class Block : ImmovableObject
    {
        public enum Face
        {
            Top = 0,
            Left,
            Bottom,
            Right,
        }

        /*public enum OpenMethod
        {
            AllOpened = 0b_0000,

            OnlyTopClosed = 0b_0001,
            OnlyLeftClosed = 0b_0010,
            OnlyBottomClosed = 0b_0100,
            OnlyRightClosed = 0b_1000,

            TopLeftClosed = 0b_0011,
            LeftBottomClosed = 0b_0110,
            BottomRightClosed = 0b_1100,
            RightTopClosed = 0b_1001,

            VerticalOpened = 0b_0101,
            HorizontalOpened = 0b_1010,

            OnlyTopOpened = 0b_1110,
            OnlyLeftOpened = 0b_1101,
            OnlyBottomOpened = 0b_1011,
            OnlyRightOpened = 0b_0111,

            AllClosed = 0b_1111,
        }*/

        private static readonly int _MinContactNum = 0;
        private static readonly int _MaxContactNum = 4;

        private static readonly Vector2[] _Normals = [ 
            Vector2.Normalize(new(0.0f, 1.0f)),
            Vector2.Normalize(new(-1.0f, 0.0f)),
            Vector2.Normalize(new(0.0f, -1.0f)),
            Vector2.Normalize(new(1.0f, 0.0f)),
            ];
        public static Vector2 GetNormal(Face face)
        {
            int i = (int)face;
            return _Normals[i];
        }

        public static readonly float SideLength = 1.0f;  // m
        public static readonly float Radius = SideLength / 2;

        private int _contactCount = _MinContactNum;
        /*public int ContactCount { get { return _contactCount; } }*/

        /*private readonly uint[] _Bitmasks = [
            0b_0001, 0b_0010, 0b_0100, 0b_1000,
            ];
        private uint _bitmask = 0b_0000;*/

        // TODO: Get position parameter as integers.
        public Block(int x, int y) : base(new(x, y)) 
        {
            Debug.Assert(_Normals.Length == _MaxContactNum);
        }

        public (int, int) GetLocation()
        {
            return ((int)Position.X, (int)Position.Y);
        }

        public (int, int) GetLocation(int x, int y)
        {
            return ((int)Position.X + x, (int)Position.Y + y);
        }

        public override void G1(Window window)
        {
            window.DrawSquare(_p, SideLength);
        }

        /*private uint _GetBitmask(Face face)
        {
            int i = (int)face;
            return _Bitmasks[i];
        }*/

        public void Contact()
        {
            /*Debug.Assert((_bitmask & _GetBitmask(face)) == 0);
            _bitmask |= _GetBitmask(face);*/

            Debug.Assert(_contactCount >= _MinContactNum);
            Debug.Assert(_contactCount <= _MaxContactNum);
            _contactCount++;
        }

        public void Uncontact()
        {
            /*Debug.Assert((_bitmask & _GetBitmask(face)) != 0);
            _bitmask ^= _GetBitmask(face);*/

            Debug.Assert(_contactCount >= _MinContactNum);
            Debug.Assert(_contactCount <= _MaxContactNum);
            _contactCount--;
        }

        /*public OpenMethod WhichOpened()
        {
            return (OpenMethod)_bitmask;
        }*/

        public bool IsAllClosed()
        {
            Debug.Assert(_contactCount >= _MinContactNum);
            Debug.Assert(_contactCount <= _MaxContactNum);
            return _contactCount == _MaxContactNum;
        }
    }
    
    class Game : Window
    {
        private const int _Scale = 8;

        private Queue<MovableObject> _movableObjects = new();
        private const int _FixedBlocksNumber = _Scale * _Scale;
        private Block[] _blocks = new Block[_FixedBlocksNumber];

        private static int _GetIndex(int x, int y)
        {
            Debug.Assert(x >= 0 && x < _Scale);
            Debug.Assert(y >= 0 && y < _Scale);
            return (y * _Scale) + x;
        }

        private static int _GetIndex(float x, float y)
        {
            var xPrime = (int)(x / _Scale);
            var yPrime = (int)(y / _Scale);
            var xPrimePrime = (int)x - (_Scale * xPrime);
            var yPrimePrime = (int)y - (_Scale * yPrime);
            return _GetIndex(xPrimePrime, yPrimePrime);
        }

        private static int _GetIndex(Vector2 position)
        {
            return _GetIndex(position.X, position.Y);
        }

        private void _UncontactBlock(int x, int y)
        {
            throw new NotImplementedException();
        }

        private void _ContactBlock(int x, int y)
        {
            var i = _GetIndex(x, y);
            var block = _blocks[i];
            if (block == null)
                return;

            block.Contact();
        }

        private void _RemoveBlock()
        {
            throw new NotImplementedException();
        }


        private void _PlaceBlock(Block block)
        {
            int x = (int)block.Position.X, y = (int)block.Position.Y;
            int i = _GetIndex(x, y);
            Debug.Assert(i >= 0 && i < _FixedBlocksNumber);

            Debug.Assert(_blocks[i] == null);
            _blocks[i] = block;

            _ContactBlock(x, y + 1);
            _ContactBlock(x - 1, y);
            _ContactBlock(x, y - 1);
            _ContactBlock(x + 1, y);

        }

        public Game(params Object[] objs) : base(800, 800, 0.01f)
        {
            Debug.Assert(_Scale > 0);

            // TODO:
            // Check the objects were overlaped.
            // If they were overlaped, adjust the positions.
            // CollisionResolution 과 같이 거리조정하는 기능도 추가하여 사용합니다.
            foreach (Object _obj in objs)
            {
                if (_obj is MovableObject movableObject)
                {
                    _movableObjects.Enqueue(movableObject);
                }
                else if (_obj is Block block)
                {
                    _PlaceBlock(block);
                }
                else
                    throw new NotImplementedException();
            }
        }
        
        protected override void Update(float dt)
        {
            /*Console.WriteLine(dt);*/
            Debug.Assert(dt > 0);

            int movableObjectsLength = _movableObjects.Count(), immovableObjectsLength;
            for (int i = 0; i < movableObjectsLength; ++i)
            {
                MovableObject movableObject = _movableObjects.Dequeue();

                movableObject.G1(this);

                movableObject.F1(dt);

                // TODO:
                // Write the code here to check a new added
                // movable object is overlaped by another object.
                // If they were overlaped, adjust the positions.

                if (movableObject.F2(MinX, MinY, MaxX, MaxY))
                    continue;

                _movableObjects.Enqueue(movableObject);
            }

            // TODO:
            // Add the new object dynamically
            // after checking the object was overlapped.

            MovableObject[] movableObjects = _movableObjects.ToArray();
            movableObjectsLength = movableObjects.Length;

            ImmovableObject[] immovableObjects = _blocks.Values;
            immovableObjectsLength = _blocks.Count;

            int objectsLength = movableObjectsLength + immovableObjectsLength;
            uint[] indices = new uint[objectsLength];
            float[] distances = new float[objectsLength];
            Vector2[] vectors = new Vector2[objectsLength];
            bool[] flags = new bool[objectsLength];
            Array.Clear(flags, 0, objectsLength);

            for (uint i = 0; i < movableObjectsLength; ++i)
            {
                MovableObject movableObject1 = movableObjects[i];
                for (uint j = i + 1; j < objectsLength; ++j)
                {
                    Object object2;
                    if (j >= movableObjectsLength)
                    {
                        object2 = _blocks
                    }
                    else
                    {
                        object2 = movableObjects[j];
                    }

                    (bool collide, float d, Vector2 u) = CollisionDetector.IsCollided(movableObject1, object2);

                    if (collide == false) continue;

                    bool fi = flags[i], fj = flags[j];
                    float di, dj;

                    if (fi == true && fj == true)
                    {
                        di = distances[i];
                        if (di >= d) continue;

                        dj = distances[j];
                        if (dj >= d) continue;

                        uint iPrime = indices[i], jPrime = indices[j];
                        flags[iPrime] = false; flags[jPrime] = false;

                        Debug.Assert(flags[i] == true);
                        Debug.Assert(flags[j] == true);
                    }
                    else if (fi == true)
                    {
                        Debug.Assert(fj == false);

                        di = distances[i];
                        if (di >= d) continue;

                        uint iPrime = indices[i];
                        flags[iPrime] = false;

                        Debug.Assert(flags[i] == true);
                        flags[j] = true;
                    }
                    else if (fj == true)
                    {
                        Debug.Assert(fi == false);

                        dj = distances[j];
                        if (dj >= d) continue;

                        uint jPrime = indices[j];
                        flags[jPrime] = false;

                        flags[i] = true;
                        Debug.Assert(flags[j] == true);
                    }
                    else
                    {
                        flags[i] = true; flags[j] = true;
                    }

                    indices[i] = j; indices[j] = i;
                    distances[i] = distances[j] = d;
                    vectors[i] = u;

                    // It is not needed, because the vector u is already had at i.
                    /*vectors[j] = u;*/

                }
            }

            /*foreach (float d in distances)
                Console.Write($"{d}, ");
            Console.WriteLine();*/

            for (uint i = 0; i < movableObjectsLength; ++i)
            {
                if (flags[i] == false) continue;

                Object obj1 = movableObjects[i];
                uint j = indices[i];
                Object obj2 = movableObjects[j];

                float d = distances[i];
                Debug.Assert(distances[j] == d);
                Vector2 u = vectors[i];
                CollisionResolution.Handle(obj1, obj2, d, u);

                /*flags[i] = false;*/  // It is not used forever.
                flags[j] = false;
            }

        }

    }

    class SimpleTwoBallsPlainCollisionSimulator
    {
        /*
         * Get Post-collision veloticies, v1' and v2', in Linear.
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

        public static void Main()
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
                new Ball(new(5.0f, 4.3f), new(-0.5f, -2.0f), 1.65f, 0.5f),
                new Ball(new(5.0f, 2.5f), new(0.0f, -2.0f), 1.8f, 0.4f),
                new Ball(new(5.0f, 4.0f), new(3.0f, 1.0f), 0.3f, 0.3f),
                new Ball(new(2.0f, 4.1f), new(3.0f, 1.0f), 0.3f, 0.3f),
                new Block(0, 4),
                new Block(0, 3),
                new Block(0, 2),
                new Block(0, 1),
                new Block(0, 0),
                new Block(1, 0),
                new Block(2, 0),
                new Block(3, 0),
                new Block(4, 0),
                new Block(5, 0),
                new Block(6, 0),
                new Block(7, 0),
                new Block(7, 1),
                new Block(7, 2),
                new Block(7, 3),
                new Block(7, 4));
            game.Run();
        }
    }
}