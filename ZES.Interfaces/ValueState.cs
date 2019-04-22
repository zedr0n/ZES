namespace ZES.Interfaces
{
    public class ValueState<T>
        where T : new()
    {
        public ValueState()
        {
            Value = new T();
        }
        
        public T Value { get; set; }
    }
}