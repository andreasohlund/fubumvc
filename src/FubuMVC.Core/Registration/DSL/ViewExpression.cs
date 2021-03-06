using System;
using System.Collections.Generic;
using System.Linq;
using FubuMVC.Core.Registration.Nodes;
using FubuMVC.Core.Runtime.Conditionals;
using FubuMVC.Core.View;
using FubuMVC.Core.View.Activation;

namespace FubuMVC.Core.Registration.DSL
{
    public class ViewExpression
    {
        private readonly ConfigurationGraph _configuration;
        private readonly FubuRegistry _registry;

        public ViewExpression(ConfigurationGraph configuration, FubuRegistry registry)
        {
            _configuration = configuration;
            _registry = registry;
        }

        /// <summary>
        ///   Register a view facility.
        /// </summary>
        public ViewExpression Facility(IViewFacility facility)
        {
            _configuration.AddFacility(facility);
            return this;
        }

        /// <summary>
        ///   Configure actionless views for view tokens matching the specified filter
        /// </summary>
        [Obsolete("This really is not necessary.  Unattached views are automatically added as 'Actionless View' chains now")]
        public ViewExpression RegisterActionLessViews(Func<IViewToken, bool> viewTokenFilter)
        {
            return RegisterActionLessViews(viewTokenFilter, chain => { chain.IsPartialOnly = true; });
        }

        /// <summary>
        ///   Specify which views should be treated as actionless views.
        /// </summary>
        /// <param name = "viewTokenFilter"></param>
        /// <param name = "configureChain">Continuation for configuring each generated <see cref = "BehaviorChain" /></param>
        /// <returns></returns>
        [Obsolete("This really is not necessary.  Unattached views are automatically added as 'Actionless View' chains now")]
        public ViewExpression RegisterActionLessViews(Func<IViewToken, bool> viewTokenFilter,
                                                      Action<BehaviorChain> configureChain)
        {
            _configuration.AddConfiguration(new ActionLessViewConvention(viewTokenFilter, configureChain),
                                            ConfigurationType.Discovery);
            return this;
        }

        /// <summary>
        ///   Specify which views should be treated as actionless views.
        /// </summary>
        /// <param name = "viewTokenFilter"></param>
        /// <param name = "configureChain">Continuation for configuring each generated <see cref = "BehaviorChain" />, depending on the corresponding view token</param>
        /// <returns></returns>
        [Obsolete("This really is not necessary.  Unattached views are automatically added as 'Actionless View' chains now")]
        public ViewExpression RegisterActionLessViews(Func<IViewToken, bool> viewTokenFilter,
                                                      Action<BehaviorChain, IViewToken> configureChain)
        {
            _configuration.AddConfiguration(new ActionLessViewConvention(viewTokenFilter, configureChain),
                                            ConfigurationType.Discovery);
            return this;
        }

        /// <summary>
        ///   Fine-tune the view attachment instead of using <see cref = "TryToAttachWithDefaultConventions" />
        /// </summary>
        public ViewExpression TryToAttach(Action<ViewsForActionFilterExpression> configure)
        {
            var expression = new ViewsForActionFilterExpression(_configuration.Views);
            configure(expression);

            return this;
        }

        /// <summary>
        ///   Configures the view attachment mechanism with default conventions:
        ///   a) by_ViewModel_and_Namespace_and_MethodName
        ///   b) by_ViewModel_and_Namespace
        ///   c) by_ViewModel
        /// </summary>
        /// <returns></returns>
        public ViewExpression TryToAttachWithDefaultConventions()
        {
            return TryToAttach(x =>
            {
                x.by_ViewModel_and_Namespace_and_MethodName();
                x.by_ViewModel_and_Namespace();
                x.by_ViewModel();
            });
        }


        /// <summary>
        ///   Define a view activation policy for views matching the filter.
        ///   <seealso cref = "IfTheInputModelOfTheViewMatches" />
        /// </summary>
        public PageActivationExpression IfTheViewTypeMatches(Func<Type, bool> filter)
        {
            Action<IPageActivationSource> registration = source => _registry.Services(x => x.AddService(source));
            return new PageActivationExpression(registration, filter);
        }

        /// <summary>
        ///   Define a view activation policy by matching on the input type of a view.
        ///   A view activation element implements <see cref = "IPageActivationAction" /> and takes part in setting up a View instance correctly
        ///   at runtime.
        /// </summary>
        public PageActivationExpression IfTheInputModelOfTheViewMatches(Func<Type, bool> filter)
        {
            Func<Type, bool> combined = type =>
            {
                var inputType = type.InputModel();
                return inputType == null ? false : filter(inputType);
            };

            return IfTheViewTypeMatches(combined);
        }

        /// <summary>
        ///   This creates a view profile for the view attachment.  Used for scenarios like
        ///   attaching multiple views to the same chain for different devices.
        /// </summary>
        /// <typeparam name = "T"></typeparam>
        /// <param name = "prefix"></param>
        /// <example>
        ///   Profile<IsMobile>("m.") -- where "m" would mean look for views that are named "m.something"
        /// </example>
        /// <returns></returns>
        public ViewExpression Profile<T>(string prefix) where T : IConditional
        {
            Func<IViewToken, string> naming = view =>
            {
                var name = view.Name();
                return name.Substring(prefix.Length);
            };
            _configuration.Views.AddProfile(typeof (T), x => x.Name().StartsWith(prefix), naming);
            return this;
        }
    }

    [Policy, Obsolete("May be completely superseeded by the fix to GH-301")]
    public class ActionLessViewConvention : IConfigurationAction
    {
        private readonly Action<BehaviorChain, IViewToken> _configureChain;
        private readonly Func<IViewToken, bool> _viewTokenFilter;

        public ActionLessViewConvention(Func<IViewToken, bool> viewTokenFilter, Action<BehaviorChain> configureChain)
        {
            _viewTokenFilter = viewTokenFilter;
            _configureChain = (chain, token) => configureChain(chain);
        }

        public ActionLessViewConvention(Func<IViewToken, bool> viewTokenFilter,
                                        Action<BehaviorChain, IViewToken> configureChain)
        {
            _viewTokenFilter = viewTokenFilter;
            _configureChain = configureChain;
        }

        public void Configure(BehaviorGraph graph)
        {
            graph.Views
                .Views
                .Where(token => _viewTokenFilter(token))
                .Each(token =>
                {
                    var chain = BehaviorChain.ForWriter(new ViewNode(token));
                    graph.AddChain(chain);

                    _configureChain(chain, token);
                });
        }
    }
}