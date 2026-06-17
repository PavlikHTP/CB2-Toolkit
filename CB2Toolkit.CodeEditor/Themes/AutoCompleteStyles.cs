namespace CB2Toolkit.CodeEditor.Themes;

public static class AutoCompleteStyles
{
    public static string GetListBoxStyleXml() =>
        @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBox'>
    <Setter Property='Background' Value='#1E1E1E'/>
    <Setter Property='Foreground' Value='#D4D4D4'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='0'/>
    <Setter Property='Margin' Value='0'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='ItemTemplate'>
        <Setter.Value>
            <DataTemplate>
                <TextBlock Text='{Binding Text}' VerticalAlignment='Center' HorizontalAlignment='Left' Margin='0'/>
            </DataTemplate>
        </Setter.Value>
    </Setter>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='ListBox'>
                <Border Background='{TemplateBinding Background}' BorderThickness='0' Padding='0' Margin='0'>
                    <ScrollViewer SnapsToDevicePixels='True' HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto' Padding='0' Margin='0'>
                        <ScrollViewer.Resources>
                            <Style TargetType='ScrollBar'>
                                <Setter Property='Background' Value='#1E1E1E'/>
                                <Setter Property='Width' Value='8'/>
                                <Setter Property='Template'>
                                    <Setter.Value>
                                        <ControlTemplate TargetType='ScrollBar'>
                                            <Grid Background='#1E1E1E'>
                                                <Track Name='PART_Track' IsDirectionReversed='True'>
                                                    <Track.Thumb>
                                                        <Thumb>
                                                            <Thumb.Template>
                                                                <ControlTemplate TargetType='Thumb'>
                                                                    <Border Background='#4E4E4E' CornerRadius='4'/>
                                                                </ControlTemplate>
                                                            </Thumb.Template>
                                                        </Thumb>
                                                    </Track.Thumb>
                                                </Track>
                                            </Grid>
                                        </ControlTemplate>
                                    </Setter.Value>
                                </Setter>
                            </Style>
                        </ScrollViewer.Resources>
                        <ItemsPresenter Margin='0' SnapsToDevicePixels='True'/>
                    </ScrollViewer>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";

    public static string GetItemStyleXml() => @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Padding' Value='4,3,6,3'/> 
    <Setter Property='FontFamily' Value='Consolas'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='HorizontalContentAlignment' Value='Left'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='ListBoxItem'>
                <Border Name='Bd' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='true'>
                    <ContentPresenter VerticalAlignment='Center' HorizontalAlignment='Left'/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property='IsSelected' Value='true'>
                        <Setter TargetName='Bd' Property='Border.Background' Value='#2D2D30'/>
                        <Setter Property='ListBoxItem.Foreground' Value='#FFFFFF'/>
                    </Trigger>
                    <Trigger Property='IsMouseOver' Value='true'>
                        <Setter TargetName='Bd' Property='Border.Background' Value='#3F3F46'/>
                        <Setter Property='ListBoxItem.Foreground' Value='#FFFFFF'/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
}