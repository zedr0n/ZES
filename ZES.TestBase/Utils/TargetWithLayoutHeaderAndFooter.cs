using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace ZES.TestBase.Utils 
{
    /// <summary>
    /// Represents a specialized NLog target that supports configurable header and footer layout
    /// along with the main log message layout. This abstract base class enables logging targets
    /// to define additional logging behavior, with support for rendering a preamble (header)
    /// and postscript (footer) around log messages.
    /// </summary>
    /// <remarks>
    /// This class extends the functionality provided by <see cref="NLog.Targets.TargetWithLayout"/>
    /// by introducing the concept of a combined header, footer, and main layout for log messages.
    /// It utilizes the <see cref="NLog.Layouts.LayoutWithHeaderAndFooter"/> component for handling
    /// the layout structure while maintaining compatibility with the standard NLog layout system.
    /// </remarks>
    public abstract class TargetWithLayoutHeaderAndFooter : TargetWithLayout
    {
        /// <summary>Gets or sets the text to be rendered.</summary>
        /// <docgen category="Layout Options" order="1" />
        [RequiredParameter]
        public override Layout Layout
        {
            get => LHF.Layout;
            set
            {
                if (value is LayoutWithHeaderAndFooter)
                    base.Layout = value;
                else if (LHF == null)
                    LHF = new LayoutWithHeaderAndFooter { Layout = value };
                else
                    LHF.Layout = value;
            }
        }

        /// <summary>Gets the footer layout to be rendered after the main log messages.</summary>
        /// <remarks>
        /// The footer layout is used to define a postscript or concluding segment of the log output, which is rendered
        /// after all log messages. It is especially useful for adding structured endings or summaries to log files.
        /// </remarks>
        /// <docgen category="Layout Options" order="2" />
        public Layout Footer => LHF.Footer;

        /// <summary>Gets the layout used for rendering the header of the log output.</summary>
        /// <remarks>
        /// The header layout is rendered once before the main log messages are processed.
        /// It is useful for adding a preamble or context to the log output.
        /// </remarks>
        /// <docgen category="Layout Options" order="1" />
        public Layout Header => LHF.Header;

        /// <summary>Gets or sets the combined layout structure, which includes the main log message layout, as well as optional header and footer layouts.</summary>
        /// <remarks>
        /// This property encapsulates an instance of <see cref="NLog.Layouts.LayoutWithHeaderAndFooter"/>
        /// to provide a unified layout handling mechanism. It allows customization of the log message's
        /// preamble (header) and postscript (footer), in addition to the core layout.
        /// </remarks>
        /// <docgen category="Layout Options" order="2" />
        private LayoutWithHeaderAndFooter LHF
        {
            get => (LayoutWithHeaderAndFooter)base.Layout;
            set => base.Layout = value;
        }
    }
}