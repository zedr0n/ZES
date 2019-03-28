using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace ZES.Tests 
{
    public abstract class TargetWithLayoutHeaderAndFooter : TargetWithLayout
    {
        /// <summary>Gets or sets the text to be rendered.</summary>
        /// <docgen category="Layout Options" order="1" />
        [RequiredParameter]
        public override Layout Layout
        {
            get
            {
                return this.LHF.Layout;
            }
            set
            {
                if (value is LayoutWithHeaderAndFooter)
                    base.Layout = value;
                else if (this.LHF == null)
                    this.LHF = new LayoutWithHeaderAndFooter()
                    {
                        Layout = value
                    };
                else
                    this.LHF.Layout = value;
            }
        }

        /// <summary>Gets or sets the footer.</summary>
        /// <docgen category="Layout Options" order="3" />
        public Layout Footer
        {
            get
            {
                return this.LHF.Footer;
            }
            set
            {
                this.LHF.Footer = value;
            }
        }

        /// <summary>Gets or sets the header.</summary>
        /// <docgen category="Layout Options" order="2" />
        public Layout Header
        {
            get
            {
                return this.LHF.Header;
            }
            set
            {
                this.LHF.Header = value;
            }
        }

        /// <summary>Gets or sets the layout with header and footer.</summary>
        /// <value>The layout with header and footer.</value>
        private LayoutWithHeaderAndFooter LHF
        {
            get
            {
                return (LayoutWithHeaderAndFooter) base.Layout;
            }
            set
            {
                base.Layout = (Layout) value;
            }
        }
    }
}