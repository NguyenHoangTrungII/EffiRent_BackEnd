using AutoMapper;

namespace eSales_SFA_Service.API.Mappers;

public class MapperInitialize : Profile
{
    public MapperInitialize()
    {
        //#region TaskManagement

        //CreateMap<CreateWorkingPlanCommand, PpcSyncWorkingPlan>()
        //    .ForMember(x => x.ReasonDescr, x
        //        => x.MapFrom(a => a.Content));
        //CreateMap<OmWorkingPlan, WorkingPlanViewModel>();
        //CreateMap<OmWorkWithCategory, WorkWithCategoryViewModel>();
        //CreateMap<OmWorkWithCriterion, WorkWithCriteriaViewModel>();

        //#endregion

        //#region LeaveApplication

        //CreateMap<PpcLeaveRequestForm, LeaveApplicationViewModel>();

        //#endregion

        //#region Receivable
        
        //CreateMap<SaveReceivable, PpcSyncDoc>();

        //#endregion

        //CreateMap<PdasalesOrd, PdaOrderRawModel>();
        //CreateMap<PdasalesOrdDet, PdaOrderDetRawModel>();
        //CreateMap<ArNewCustomerInfor, ArNewCustomerInforHi>();
        //CreateMap<ArNewCustomerInforEdit, ArNewCustomerInforHisEdit>();
        //CreateMap<ArNewCustomerInfor, ArNewCustomerInforHisEdit>();
        //CreateMap<ArCustomer, ArNewCustomerInforHisEdit>();

    }
}
