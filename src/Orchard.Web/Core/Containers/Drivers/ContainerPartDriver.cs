﻿using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Aspects;
using Orchard.ContentManagement.Drivers;
using Orchard.ContentManagement.Handlers;
using Orchard.ContentManagement.MetaData;
using Orchard.Core.Common.Models;
using Orchard.Core.Containers.Models;
using Orchard.Core.Containers.Settings;
using Orchard.Core.Containers.ViewModels;
using Orchard.Data;
using Orchard.Localization;
using Orchard.UI.Notify;

namespace Orchard.Core.Containers.Drivers {
    public class ContainerPartDriver : ContentPartDriver<ContainerPart> {
        private readonly IContentDefinitionManager _contentDefinitionManager;

        public ContainerPartDriver(IContentDefinitionManager contentDefinitionManager, IOrchardServices orchardServices) {
            _contentDefinitionManager = contentDefinitionManager;
            Services = orchardServices;
            T = NullLocalizer.Instance;
        }

        public IOrchardServices Services { get; private set; }
        public Localizer T { get; set; }

        protected override DriverResult Display(ContainerPart part, string displayType, dynamic shapeHelper) {
            return Combined(
                ContentShape("Parts_Container_Contained",
                             () => shapeHelper.Parts_Container_Contained(ContentPart: part)),
                ContentShape("Parts_Container_Contained_Summary",
                             () => shapeHelper.Parts_Container_Contained_Summary(ContentPart: part)),
                ContentShape("Parts_Container_Contained_SummaryAdmin",
                             () => shapeHelper.Parts_Container_Contained_SummaryAdmin(ContentPart: part))
                );
        }

        protected override DriverResult Editor(ContainerPart part, dynamic shapeHelper) {
            // if there are no containable items then show a nice little warning
            if (!_contentDefinitionManager.ListTypeDefinitions()
                .Where(typeDefinition => typeDefinition.Parts.Any(partDefinition => partDefinition.PartDefinition.Name == "ContainablePart")).Any()) {
                Services.Notifier.Warning(T("There are no content types in the system with a Containable part attached. Consider adding a Containable part to some content type, existing or new, in order to relate items to this (Container enabled) item."));
            }

            return Editor(part, (IUpdateModel)null, shapeHelper);
        }

        protected override DriverResult Editor(ContainerPart part, IUpdateModel updater, dynamic shapeHelper) {
            return ContentShape(
                "Parts_Container_Edit",
                () => {
                    var model = new ContainerViewModel { Part = part };
                    // todo: is there a non-string comparison way to find ConaintableParts?
                    var containables = _contentDefinitionManager.ListTypeDefinitions().Where(td => td.Parts.Any(p => p.PartDefinition.Name == "ContainablePart")).ToList();
                    var listItems = new[] { new SelectListItem { Text = T("(Any)").Text, Value = "" } }
                        .Concat(containables.Select(x => new SelectListItem {
                            Value = Convert.ToString(x.Name),
                            Text = x.DisplayName,
                            Selected = x.Name == model.Part.Record.ItemContentType,
                        }))
                        .ToList();

                    model.AvailableContainables = new SelectList(listItems, "Value", "Text", model.Part.Record.ItemContentType);

                    if (updater != null) {
                        updater.TryUpdateModel(model, "Container", null, null);
                    }

                    return shapeHelper.EditorTemplate(TemplateName: "Container", Model: model, Prefix: "Container");
                });
        }
    }

    public class ContainerPartHandler : ContentHandler {
        public ContainerPartHandler(IRepository<ContainerPartRecord> repository) {
            Filters.Add(StorageFilter.For(repository));
            OnInitializing<ContainerPart>((context, part) => {
                part.Record.PageSize = part.Settings.GetModel<ContainerTypePartSettings>().PageSizeDefault
                    ?? part.PartDefinition.Settings.GetModel<ContainerPartSettings>().PageSizeDefault;
                part.Record.Paginated = part.Settings.GetModel<ContainerTypePartSettings>().PaginatedDefault
                    ?? part.PartDefinition.Settings.GetModel<ContainerPartSettings>().PaginatedDefault;

                //hard-coded defaults for ordering
                part.Record.OrderByProperty = part.Is<CommonPart>() ? "CommonPart.PublishedUtc" : "";
                part.Record.OrderByDirection = (int)OrderByDirection.Descending;
            });
        }
    }
}
