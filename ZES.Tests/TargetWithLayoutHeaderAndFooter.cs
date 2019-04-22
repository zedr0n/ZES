using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
// ReSharper disable InconsistentNaming

namespace ZES.Tests 
{
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
                    LHF = new LayoutWithHeaderAndFooter
                    {
                        Layout = value
                    };
                else
                    LHF.Layout = value;
            }
        }

        public Layout Footer => LHF.Footer;

        public Layout Header => LHF.Header;

        /// <summary>Gets or sets the layout with header and footer.</summary>
        /// <value>The layout with header and footer.</value>
        private LayoutWithHeaderAndFooter LHF
        {
            get => (LayoutWithHeaderAndFooter) base.Layout;
            set => base.Layout = value;
        }
    }
}