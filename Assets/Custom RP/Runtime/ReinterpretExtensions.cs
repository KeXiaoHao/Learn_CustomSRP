using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    // 使用指定的 LayoutKind 枚举成员初始化 StructLayoutAttribute 类的新实例
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)]public int intValue;
        [FieldOffset(0)]public float floatValue;
    }
    
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue; //设置整数值 返回浮点值
    }
    // 现在 结构体的字段表示相同的数据 但解释不同
    // 这样可以保持bit掩码不变 并且渲染层掩码现在可以正常工作
}
