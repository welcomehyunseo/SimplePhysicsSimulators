namespace Printer
{
    public class HelloPrinter
    {
        public HelloPrinter() { }
        ~HelloPrinter() { }

        public void Print() 
        {
            Console.WriteLine("Hello!!");
        }
    }

    public class ByePrinter
    {
        public ByePrinter() { }
        ~ByePrinter() { }

        public void Print()
        {
            Console.WriteLine("Bye!!");
        }
    }
}
